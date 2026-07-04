using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Generators.StateTransition;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Immutable contract-level metadata extracted from a registration attribute.
/// Used as the grouping key in <see cref="RegistrationGeneratorHelper.Execute"/>.
/// </summary>
internal sealed record ContractInfo(
    string FullyQualifiedName,
    string Name,
    string? Namespace,
    string FullyQualifiedDisplayString);

/// <summary>
/// Immutable registration model collected by the incremental pipeline.
/// All fields are value-equatable to ensure correct incremental caching.
/// </summary>
internal sealed record KeyedRegistration(
    string Key,
    ContractInfo Contract,
    string ImplementationName,
    string ImplementationFullyQualifiedDisplayString,
    bool ImplementsContract,
    bool HasPublicParameterlessConstructor,
    GuardResolution Guard,
    bool IsAsync,
    bool ImplementsAsyncFactory,
    int PoolSize,
    LocationInfo Location);

internal static class RegistrationGeneratorHelper
{
    internal static EquatableArray<KeyedRegistration> Transform(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        var result = new List<KeyedRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol implementation)
        {
            return new EquatableArray<KeyedRegistration>(result.ToArray());
        }

        foreach (var attribute in context.Attributes)
        {
            var key = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            INamedTypeSymbol? contract = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    contract = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 1)
            {
                contract = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
            }

            if (contract is null || contract.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var contractInfo = new ContractInfo(
                contract.ToDisplayString(),
                contract.Name,
                contract.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : contract.ContainingNamespace.ToDisplayString(),
                contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            var location = new LocationInfo(context.TargetNode.GetLocation());

            // Resolve optional Guard property from the attribute.
            string? guardName = null;
            bool isAsync = false;
            int poolSize = 0;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (string.Equals(namedArg.Key, "Guard", StringComparison.Ordinal)
                    && namedArg.Value.Value is string guardValue)
                {
                    guardName = guardValue;
                }
                else if (string.Equals(namedArg.Key, "IsAsync", StringComparison.Ordinal)
                    && namedArg.Value.Value is bool isAsyncValue)
                {
                    isAsync = isAsyncValue;
                }
                else if (string.Equals(namedArg.Key, "PoolSize", StringComparison.Ordinal)
                    && namedArg.Value.Value is int poolSizeValue)
                {
                    poolSize = poolSizeValue;
                }
            }

            var guard = guardName is null
                ? default(GuardResolution)
                : GuardMethodValidator.Resolve(
                    implementation,
                    guardName,
                    ImmutableArray.Create<ITypeSymbol>(
                        context.SemanticModel.Compilation.GetSpecialType(SpecialType.System_String)));

            var implementsAsyncFactory = ImplementsAsyncFactory(implementation, contract);

            result.Add(new KeyedRegistration(
                key!,
                contractInfo,
                implementation.Name,
                implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ImplementsContract(implementation, contract),
                HasPublicParameterlessConstructor(implementation),
                guard,
                isAsync,
                implementsAsyncFactory,
                poolSize,
                location));
        }

        return new EquatableArray<KeyedRegistration>(result.ToArray());
    }

    internal static void Execute(
        SourceProductionContext context,
        ImmutableArray<KeyedRegistration> nonGeneric,
        ImmutableArray<KeyedRegistration> generic,
        GeneratorIntegrationOptions integrationOptions,
        KeyedRegistrationDiagnostics diagnostics,
        Action<SourceProductionContext, ContractInfo, List<KeyedRegistration>, GeneratorIntegrationOptions, bool> emitGeneratedSources)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .ToList();

        if (registrations.Count == 0)
        {
            return;
        }

        var contractNamesWithCollisions = new HashSet<string>(
            registrations
                .Select(static r => r.Contract)
                .Distinct()
                .GroupBy(static c => c.Name, StringComparer.Ordinal)
                .Where(static g => g.Count() > 1)
                .Select(static g => g.Key),
            StringComparer.Ordinal);

        foreach (var group in registrations.GroupBy(static r => r.Contract.FullyQualifiedName, StringComparer.Ordinal))
        {
            var contract = group.First().Contract;
            var contractRegistrations = group.ToList();

            ReportDuplicateKeys(context, contractRegistrations, diagnostics.DuplicateKey);
            ReportContractMismatches(context, contractRegistrations, diagnostics.ContractMismatch);
            ReportMissingConstructors(context, contractRegistrations, diagnostics.MissingParameterlessConstructor);
            ReportGuardDiagnostics(context, contractRegistrations);
            ReportFactoryAsyncDiagnostics(context, contractRegistrations);

            var valid = contractRegistrations
                .Where(static r => r.ImplementsContract)
                .Where(static r => r.HasPublicParameterlessConstructor)
                .Where(static r => r.Guard.IsValid)
                .GroupBy(static r => r.Key, StringComparer.Ordinal)
                .Where(static g => g.Count() == 1)
                .Select(static g => g.First())
                .OrderBy(static r => r.Key, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            emitGeneratedSources(
                context,
                contract,
                valid,
                integrationOptions,
                contractNamesWithCollisions.Contains(contract.Name));
        }
    }

    private static void ReportDuplicateKeys(
        SourceProductionContext context,
        List<KeyedRegistration> registrations,
        DiagnosticDescriptor descriptor)
    {
        foreach (var duplicateGroup in registrations.GroupBy(static r => r.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var contractName = duplicateGroup.First().Contract.FullyQualifiedName;
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    registration.Location.ToLocation(),
                    registration.Key,
                    contractName));
            }
        }
    }

    private static void ReportContractMismatches(
        SourceProductionContext context,
        List<KeyedRegistration> registrations,
        DiagnosticDescriptor descriptor)
    {
        foreach (var registration in registrations.Where(static r => !r.ImplementsContract))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                registration.Location.ToLocation(),
                registration.ImplementationName,
                registration.Contract.FullyQualifiedName));
        }
    }

    private static void ReportMissingConstructors(
        SourceProductionContext context,
        List<KeyedRegistration> registrations,
        DiagnosticDescriptor descriptor)
    {
        foreach (var registration in registrations.Where(static r => !r.HasPublicParameterlessConstructor))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                registration.Location.ToLocation(),
                registration.ImplementationName));
        }
    }

    private static void ReportGuardDiagnostics(
        SourceProductionContext context,
        List<KeyedRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            var guard = registration.Guard;
            if (guard.IsValid)
            {
                continue;
            }

            var keyTypeDisplay = "string";

            if (!guard.IsFound)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StrategyGuardMethodNotFound,
                    registration.Location.ToLocation(),
                    guard.Name,
                    registration.ImplementationName,
                    keyTypeDisplay));
                continue;
            }

            if (!guard.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StrategyGuardMethodNotStatic,
                    registration.Location.ToLocation(),
                    guard.Name,
                    registration.ImplementationName));
                continue;
            }

            if (!guard.HasValidSignature)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StrategyGuardMethodWrongSignature,
                    registration.Location.ToLocation(),
                    guard.Name,
                    registration.ImplementationName,
                    keyTypeDisplay));
            }
        }
    }

    private static void ReportFactoryAsyncDiagnostics(
        SourceProductionContext context,
        List<KeyedRegistration> registrations)
    {
        const int LargePoolSizeThreshold = 1024;

        foreach (var registration in registrations)
        {
            // DP053: IsAsync=true but does not implement IAsyncFactory<T>
            if (registration.IsAsync && !registration.ImplementsAsyncFactory)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.FactoryAsyncSignatureMismatch,
                    registration.Location.ToLocation(),
                    registration.ImplementationName,
                    registration.Contract.FullyQualifiedName));
            }

            // DP054: PoolSize < 0
            if (registration.PoolSize < 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.FactoryPoolSizeInvalid,
                    registration.Location.ToLocation(),
                    registration.ImplementationName,
                    registration.PoolSize));
            }

            // DP055: PoolSize > 1024 (warning)
            if (registration.PoolSize > LargePoolSizeThreshold)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.FactoryPoolSizeTooLarge,
                    registration.Location.ToLocation(),
                    registration.ImplementationName,
                    registration.PoolSize));
            }
        }
    }

    internal static bool ImplementsContract(INamedTypeSymbol implementation, INamedTypeSymbol contract)
    {
        if (SymbolEqualityComparer.Default.Equals(implementation, contract))
        {
            return true;
        }

        foreach (var iface in implementation.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, contract))
            {
                return true;
            }
        }

        for (var baseType = implementation.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, contract))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasPublicParameterlessConstructor(INamedTypeSymbol implementation) =>
        implementation.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);

    /// <summary>
    /// Checks whether <paramref name="implementation"/> implements <c>IAsyncFactory&lt;T&gt;</c>
    /// where the type argument matches <paramref name="contract"/>.
    /// </summary>
    internal static bool ImplementsAsyncFactory(INamedTypeSymbol implementation, INamedTypeSymbol contract)
    {
        foreach (var iface in implementation.AllInterfaces)
        {
            if (iface.Name != "IAsyncFactory" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], contract))
            {
                return true;
            }
        }

        return false;
    }
}
