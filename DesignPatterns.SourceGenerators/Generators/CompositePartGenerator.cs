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

    private static readonly DiagnosticDescriptor DuplicateKeyDescriptor = new(
        id: DiagnosticIds.CompositePartDuplicateKey,
        title: "Duplicate composite key",
        messageFormat: "Composite key '{0}' is already registered for contract '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownParentKeyDescriptor = new(
        id: DiagnosticIds.CompositePartUnknownParentKey,
        title: "Unknown composite parent key",
        messageFormat: "Composite parent key '{0}' was not found for contract '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CycleDescriptor = new(
        id: DiagnosticIds.CompositePartCycle,
        title: "Composite parent chain cycle",
        messageFormat: "Composite key '{0}' participates in a parent-key cycle for contract '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ContractMismatchDescriptor = new(
        id: DiagnosticIds.CompositePartContractMismatch,
        title: "Composite part does not implement contract",
        messageFormat: "Type '{0}' does not implement composite contract '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingParameterlessConstructorDescriptor = new(
        id: DiagnosticIds.CompositePartMissingParameterlessConstructor,
        title: "Composite part requires a public parameterless constructor",
        messageFormat: "Type '{0}' must declare a public parameterless constructor to be used with generated composite catalogs",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingBuildableDescriptor = new(
        id: DiagnosticIds.CompositePartMissingBuildable,
        title: "Composite part must implement ICompositeBuildable",
        messageFormat: "Type '{0}' must implement ICompositeBuildable<{1}> to be used with generated composite catalogs",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            CompositePartMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false));

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            CompositePartGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true));

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static CompositeRegistration? Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
    {
        if (context.TargetSymbol is not INamedTypeSymbol implementation)
        {
            return null;
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return null;
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

            return new CompositeRegistration(
                key!,
                parentKey,
                order,
                contract,
                implementation,
                context.TargetNode.GetLocation());
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<CompositeRegistration?> nonGeneric,
        ImmutableArray<CompositeRegistration?> generic)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .Where(static r => r is not null)
            .Cast<CompositeRegistration>()
            .ToList();

        if (registrations.Count == 0)
        {
            return;
        }

        foreach (var group in registrations.GroupBy(static r => r.Contract, SymbolEqualityComparer.Default))
        {
            if (group.Key is not INamedTypeSymbol contract)
            {
                continue;
            }

            var contractRegistrations = group.ToList();
            ReportDuplicateKeys(context, contractRegistrations);
            ReportUnknownParentKeys(context, contractRegistrations);
            ReportCycles(context, contractRegistrations);
            ReportContractMismatches(context, contractRegistrations);
            ReportMissingConstructors(context, contractRegistrations);
            ReportMissingBuildable(context, contractRegistrations, contract);

            var valid = contractRegistrations
                .Where(r => ImplementsContract(r.Implementation, contract))
                .Where(r => HasPublicParameterlessConstructor(r.Implementation))
                .Where(r => ImplementsBuildable(r.Implementation, contract))
                .GroupBy(r => r.Key, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .Where(r => IsParentKeyValid(r, contractRegistrations))
                .Where(r => !ParticipatesInCycle(r.Key, contractRegistrations))
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitGeneratedSources(context, contract, valid);
        }
    }

    private static void ReportDuplicateKeys(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        foreach (var duplicateGroup in registrations.GroupBy(static r => r.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var contractName = duplicateGroup.First().Contract.ToDisplayString();
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
            registrations.Select(static r => r.Key),
            StringComparer.Ordinal);
        var contractName = registrations.FirstOrDefault()?.Contract.ToDisplayString() ?? string.Empty;

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
        var contractName = registrations.FirstOrDefault()?.Contract.ToDisplayString() ?? string.Empty;
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
        foreach (var registration in registrations.Where(r => !ImplementsContract(r.Implementation, r.Contract)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ContractMismatchDescriptor,
                registration.Location,
                registration.Implementation.Name,
                registration.Contract.ToDisplayString()));
        }
    }

    private static void ReportMissingConstructors(SourceProductionContext context, List<CompositeRegistration> registrations)
    {
        foreach (var registration in registrations.Where(r => !HasPublicParameterlessConstructor(r.Implementation)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingParameterlessConstructorDescriptor,
                registration.Location,
                registration.Implementation.Name));
        }
    }

    private static void ReportMissingBuildable(
        SourceProductionContext context,
        List<CompositeRegistration> registrations,
        INamedTypeSymbol contract)
    {
        foreach (var registration in registrations.Where(r => !ImplementsBuildable(r.Implementation, contract)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingBuildableDescriptor,
                registration.Location,
                registration.Implementation.Name,
                contract.ToDisplayString()));
        }
    }

    private static void EmitGeneratedSources(
        SourceProductionContext context,
        INamedTypeSymbol contract,
        List<CompositeRegistration> registrations)
    {
        var namespaceName = contract.ContainingNamespace.IsGlobalNamespace
            ? null
            : contract.ContainingNamespace.ToDisplayString();

        var contractTypeName = contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var keysClassName = CompositeSyntaxFactory.GetKeysClassName(contract);
        var catalogClassName = CompositeSyntaxFactory.GetCatalogClassName(contract);

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
            .Select(r => (
                r.Key,
                r.ParentKey,
                r.Order,
                r.Implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToList();

        var keysUnit = CompositeSyntaxFactory.CreateKeysCompilationUnit(namespaceName, keysClassName, keys);
        var catalogUnit = CompositeSyntaxFactory.CreateCatalogCompilationUnit(
            namespaceName,
            catalogClassName,
            contractTypeName,
            catalogEntries);

        var hintPrefix = contract.Name;
        context.AddSource(
            $"{hintPrefix}.{keysClassName}.g.cs",
            SourceText.From(keysUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{catalogClassName}.g.cs",
            SourceText.From(catalogUnit.ToFullString(), Encoding.UTF8));
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

    private sealed class CompositeRegistration
    {
        public CompositeRegistration(
            string key,
            string? parentKey,
            int order,
            INamedTypeSymbol contract,
            INamedTypeSymbol implementation,
            Location location)
        {
            Key = key;
            ParentKey = parentKey;
            Order = order;
            Contract = contract;
            Implementation = implementation;
            Location = location;
        }

        public string Key { get; }

        public string? ParentKey { get; }

        public int Order { get; }

        public INamedTypeSymbol Contract { get; }

        public INamedTypeSymbol Implementation { get; }

        public Location Location { get; }
    }
}
