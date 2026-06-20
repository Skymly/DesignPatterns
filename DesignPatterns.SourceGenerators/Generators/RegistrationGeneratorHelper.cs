using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
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
    Location Location);

internal static class RegistrationGeneratorHelper
{
    internal static List<KeyedRegistration> Transform(
        GeneratorAttributeSyntaxContext context,
        bool isGenericAttribute)
    {
        var result = new List<KeyedRegistration>();

        if (context.TargetSymbol is not INamedTypeSymbol implementation)
        {
            return result;
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

            var location = context.TargetNode.GetLocation();
            result.Add(new KeyedRegistration(
                key!,
                contractInfo,
                implementation.Name,
                implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ImplementsContract(implementation, contract),
                HasPublicParameterlessConstructor(implementation),
                location));
        }

        return result;
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

            var valid = contractRegistrations
                .Where(static r => r.ImplementsContract)
                .Where(static r => r.HasPublicParameterlessConstructor)
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
                    registration.Location,
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
                registration.Location,
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
                registration.Location,
                registration.ImplementationName));
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
}
