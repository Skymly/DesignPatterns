using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates composite key constants and catalogs for <c>[CompositePart]</c> implementations.
/// </summary>
[Generator]
public sealed class CompositePartGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>CompositePartAttribute</c>.</summary>
    public const string CompositePartMetadataName = "DesignPatterns.Structural.CompositePartAttribute";

    /// <summary>Metadata name for generic <c>CompositePartAttribute&lt;TContract&gt;</c>.</summary>
    public const string CompositePartGenericMetadataName = "DesignPatterns.Structural.CompositePartAttribute`1";

    private static readonly DiagnosticDescriptor DuplicateKeyDescriptor =
        DesignPatternsDiagnosticDescriptors.CompositePartDuplicateKey;

    private static readonly DiagnosticDescriptor UnknownParentKeyDescriptor =
        DesignPatternsDiagnosticDescriptors.CompositePartUnknownParentKey;

    private static readonly DiagnosticDescriptor CycleDescriptor =
        DesignPatternsDiagnosticDescriptors.CompositePartCycle;

    private static readonly DiagnosticDescriptor ContractMismatchDescriptor =
        DesignPatternsDiagnosticDescriptors.CompositePartContractMismatch;

    private static readonly DiagnosticDescriptor MissingParameterlessConstructorDescriptor =
        DesignPatternsDiagnosticDescriptors.CompositePartMissingParameterlessConstructor;

    private static readonly DiagnosticDescriptor MissingBuildableDescriptor =
        DesignPatternsDiagnosticDescriptors.CompositePartMissingBuildable;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            CompositePartMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.CompositeNonGenericTransform);

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            CompositePartGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.CompositeGenericTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()).Combine(integrationOptions),
            static (spc, source) => Execute(spc, source.Left.Left, source.Left.Right, source.Right));
    }

    private static Result<CompositeRegistration> Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
    {
        if (context.TargetSymbol is not INamedTypeSymbol implementation)
        {
            return Result<CompositeRegistration>.Empty;
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return Result<CompositeRegistration>.Empty;
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

            string? parentKey = null;
            var order = 0;
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "ParentKey" && named.Value.Value is string parent)
                {
                    parentKey = parent;
                }
                else if (named.Key == "Order" && named.Value.Value is int orderValue)
                {
                    order = orderValue;
                }
            }

            var contractInfo = new ContractInfo(
                contract.ToDisplayString(),
                contract.Name,
                contract.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : contract.ContainingNamespace.ToDisplayString(),
                contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            return Result<CompositeRegistration>.Success(new CompositeRegistration(
                key!,
                parentKey,
                order,
                contractInfo,
                implementation.Name,
                implementation.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : implementation.ContainingNamespace.ToDisplayString(),
                implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ImplementsContract(implementation, contract),
                HasPublicParameterlessConstructor(implementation),
                ImplementsBuildable(implementation, contract),
                context.TargetNode.GetLocation()));
        }

        return Result<CompositeRegistration>.Empty;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<Result<CompositeRegistration>> nonGeneric,
        ImmutableArray<Result<CompositeRegistration>> generic,
        GeneratorIntegrationOptions integrationOptions)
    {
        var registrations = ResultExtensions.ReportAndCollect(context, nonGeneric.Concat(generic));

        if (registrations.Count == 0)
        {
            return;
        }

        foreach (var group in registrations.GroupBy(static r => r.Contract.FullyQualifiedName, StringComparer.Ordinal))
        {
            var contract = group.First().Contract;
            var contractRegistrations = group.ToList();
            ReportDuplicateKeys(context, contractRegistrations);
            ReportUnknownParentKeys(context, contractRegistrations);
            ReportCycles(context, contractRegistrations);
            ReportContractMismatches(context, contractRegistrations);
            ReportMissingConstructors(context, contractRegistrations);
            ReportMissingBuildable(context, contractRegistrations, contract);

            var valid = contractRegistrations
                .Where(static r => r.ImplementsContract)
                .Where(static r => r.HasPublicParameterlessConstructor)
                .Where(static r => r.ImplementsBuildable)
                .GroupBy(static r => r.Key, StringComparer.Ordinal)
                .Where(static g => g.Count() == 1)
                .Select(static g => g.First())
                .Where(r => IsParentKeyValid(r, contractRegistrations))
                .Where(r => !ParticipatesInCycle(r.Key, contractRegistrations))
                .OrderBy(static r => r.Key, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitGeneratedSources(context, contract, valid, integrationOptions);
        }
    }

    private static void ReportDuplicateKeys(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        foreach (var duplicateGroup in registrations.GroupBy(static r => r.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var contractName = duplicateGroup.First().Contract.FullyQualifiedName;
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateKeyDescriptor,
                    registration.Location,
                    registration.Key,
                    contractName));
            }
        }
    }

    private static void ReportUnknownParentKeys(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        var keys = new HashSet<string>(
            registrations.Select(r => r.Key),
            StringComparer.Ordinal);
        var contractName = registrations.FirstOrDefault()?.Contract.FullyQualifiedName ?? string.Empty;

        foreach (var registration in registrations.Where(r => r.ParentKey is not null && !keys.Contains(r.ParentKey)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnknownParentKeyDescriptor,
                registration.Location,
                registration.ParentKey!,
                contractName));
        }
    }

    private static void ReportCycles(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        var contractName = registrations.FirstOrDefault()?.Contract.FullyQualifiedName ?? string.Empty;
        var parentByKey = BuildParentMap(registrations);

        foreach (var registration in registrations.Where(r => ParticipatesInCycle(r.Key, parentByKey)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CycleDescriptor,
                registration.Location,
                registration.Key,
                contractName));
        }
    }

    private static void ReportContractMismatches(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        foreach (var registration in registrations.Where(static r => !r.ImplementsContract))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ContractMismatchDescriptor,
                registration.Location,
                registration.ImplementationName,
                registration.Contract.FullyQualifiedName));
        }
    }

    private static void ReportMissingConstructors(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        foreach (var registration in registrations.Where(static r => !r.HasPublicParameterlessConstructor))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingParameterlessConstructorDescriptor,
                registration.Location,
                registration.ImplementationName));
        }
    }

    private static void ReportMissingBuildable(
        SourceProductionContext context,
        List<CompositeRegistration> registrations,
        ContractInfo contract)
    {
        foreach (var registration in registrations.Where(static r => !r.ImplementsBuildable))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingBuildableDescriptor,
                registration.Location,
                registration.ImplementationName,
                contract.FullyQualifiedName));
        }
    }

    private static void EmitGeneratedSources(
        SourceProductionContext context,
        ContractInfo contract,
        List<CompositeRegistration> registrations,
        GeneratorIntegrationOptions integrationOptions)
    {
        var keysClassName = CompositeSyntaxFactory.GetKeysClassName(contract.Name);
        var catalogClassName = CompositeSyntaxFactory.GetCatalogClassName(contract.Name);

        var constantNames = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<(string ConstantName, string KeyValue)>();
        foreach (var registration in registrations)
        {
            var constantName = CompositeSyntaxFactory.ToConstantName(registration.Key);
            if (!constantNames.Add(constantName))
            {
                constantName += "_" + keys.Count;
            }

            keys.Add((constantName, registration.Key));
        }

        var catalogEntries = registrations
            .Select(static r => (
                r.Key,
                r.ParentKey,
                r.Order,
                r.ImplementationFullyQualifiedDisplayString))
            .ToList();

        var keysUnit = CompositeSyntaxFactory.CreateKeysCompilationUnit(contract.Namespace, keysClassName, keys);
        var catalogUnit = CompositeSyntaxFactory.CreateCatalogCompilationUnit(
            contract.Namespace,
            catalogClassName,
            contract.FullyQualifiedDisplayString,
            catalogEntries,
            integrationOptions);

        var hintPrefix = contract.Name;
        context.AddSource(
            $"{hintPrefix}.{keysClassName}.g.cs",
            SourceText.From(keysUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{catalogClassName}.g.cs",
            SourceText.From(catalogUnit.ToFullString(), Encoding.UTF8));

        // Visitor interface + dispatch extension methods
        var visitorInterfaceName = CompositeSyntaxFactory.GetVisitorInterfaceName(contract.Name);
        var implTypes = registrations
            .Select(static r => (r.ImplementationName, r.ImplementationNamespace, r.ImplementationFullyQualifiedDisplayString))
            .ToList();

        var visitorUnit = CompositeSyntaxFactory.CreateVisitorInterfaceCompilationUnit(
            contract.Namespace,
            visitorInterfaceName,
            contract.FullyQualifiedDisplayString,
            implTypes);
        context.AddSource(
            $"{hintPrefix}.{visitorInterfaceName}.g.cs",
            SourceText.From(visitorUnit.ToFullString(), Encoding.UTF8));
    }

    private static bool IsParentKeyValid(CompositeRegistration registration, List<CompositeRegistration> registrations)
    {
        if (registration.ParentKey is null)
        {
            return true;
        }

        return registrations.Any(r => string.Equals(r.Key, registration.ParentKey, StringComparison.Ordinal));
    }

    private static bool ParticipatesInCycle(string key, List<CompositeRegistration> registrations) =>
        ParticipatesInCycle(key, BuildParentMap(registrations));

    private static IReadOnlyDictionary<string, string?> BuildParentMap(List<CompositeRegistration> registrations)
    {
        var parentByKey = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            if (!parentByKey.ContainsKey(registration.Key))
            {
                parentByKey[registration.Key] = registration.ParentKey;
            }
        }

        return parentByKey;
    }

    private static bool ParticipatesInCycle(string key, IReadOnlyDictionary<string, string?> parentByKey)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = key;

        while (parentByKey.TryGetValue(current, out var parent) && parent is not null)
        {
            if (!visited.Add(parent))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static bool ImplementsContract(INamedTypeSymbol implementation, INamedTypeSymbol contract)
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

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol implementation) =>
        implementation.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);

    private static bool ImplementsBuildable(INamedTypeSymbol implementation, INamedTypeSymbol contract)
    {
        foreach (var iface in implementation.AllInterfaces)
        {
            if (iface.Name != "ICompositeBuildable" || iface.TypeArguments.Length != 1)
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

    private sealed record CompositeRegistration(
        string Key,
        string? ParentKey,
        int Order,
        ContractInfo Contract,
        string ImplementationName,
        string? ImplementationNamespace,
        string ImplementationFullyQualifiedDisplayString,
        bool ImplementsContract,
        bool HasPublicParameterlessConstructor,
        bool ImplementsBuildable,
        Location Location);
}
