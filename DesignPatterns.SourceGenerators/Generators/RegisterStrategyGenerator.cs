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
/// Generates strategy key constants and static registries for <c>[RegisterStrategy]</c> implementations.
/// </summary>
[Generator]
public sealed class RegisterStrategyGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>RegisterStrategyAttribute</c>.</summary>
    public const string RegisterStrategyMetadataName = "DesignPatterns.Behavioral.RegisterStrategyAttribute";

    /// <summary>Metadata name for generic <c>RegisterStrategyAttribute&lt;TContract&gt;</c>.</summary>
    public const string RegisterStrategyGenericMetadataName = "DesignPatterns.Behavioral.RegisterStrategyAttribute`1";

    private static readonly DiagnosticDescriptor DuplicateKeyDescriptor = new(
        id: DiagnosticIds.RegisterStrategyDuplicateKey,
        title: "Duplicate strategy key",
        messageFormat: "Strategy key '{0}' is already registered for contract '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ContractMismatchDescriptor = new(
        id: DiagnosticIds.RegisterStrategyContractMismatch,
        title: "Strategy does not implement contract",
        messageFormat: "Type '{0}' does not implement strategy contract '{1}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingParameterlessConstructorDescriptor = new(
        id: DiagnosticIds.RegisterStrategyMissingParameterlessConstructor,
        title: "Strategy implementation requires a public parameterless constructor",
        messageFormat: "Type '{0}' must declare a public parameterless constructor to be used with generated static registration",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterStrategyMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false));

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterStrategyGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true));

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static StrategyRegistration? Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
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

            var location = context.TargetNode.GetLocation();
            return new StrategyRegistration(key!, contract, implementation, location);
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<StrategyRegistration?> nonGeneric,
        ImmutableArray<StrategyRegistration?> generic)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .Where(static r => r is not null)
            .Cast<StrategyRegistration>()
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
            ReportContractMismatches(context, contractRegistrations);
            ReportMissingConstructors(context, contractRegistrations);

            var valid = contractRegistrations
                .Where(r => ImplementsContract(r.Implementation, contract))
                .Where(r => HasPublicParameterlessConstructor(r.Implementation))
                .GroupBy(r => r.Key, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitGeneratedSources(context, contract, valid);
        }
    }

    private static void ReportDuplicateKeys(SourceProductionContext context, List<StrategyRegistration> registrations)
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

    private static void ReportContractMismatches(SourceProductionContext context, List<StrategyRegistration> registrations)
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

    private static void ReportMissingConstructors(SourceProductionContext context, List<StrategyRegistration> registrations)
    {
        foreach (var registration in registrations.Where(r => !HasPublicParameterlessConstructor(r.Implementation)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingParameterlessConstructorDescriptor,
                registration.Location,
                registration.Implementation.Name));
        }
    }

    private static void EmitGeneratedSources(
        SourceProductionContext context,
        INamedTypeSymbol contract,
        List<StrategyRegistration> registrations)
    {
        var namespaceName = contract.ContainingNamespace.IsGlobalNamespace
            ? null
            : contract.ContainingNamespace.ToDisplayString();

        var contractTypeName = contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var keysClassName = StrategySyntaxFactory.GetKeysClassName(contract);
        var registryClassName = StrategySyntaxFactory.GetRegistryClassName(contract);

        var constantNames = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<(string ConstantName, string KeyValue)>();
        foreach (var registration in registrations)
        {
            var constantName = StrategySyntaxFactory.ToConstantName(registration.Key);
            if (!constantNames.Add(constantName))
            {
                constantName += "_" + keys.Count;
            }

            keys.Add((constantName, registration.Key));
        }

        var registryEntries = registrations
            .Select(r => (r.Key, r.Implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToList();

        var keysUnit = StrategySyntaxFactory.CreateKeysCompilationUnit(namespaceName, keysClassName, keys);
        var registryUnit = StrategySyntaxFactory.CreateRegistryCompilationUnit(
            namespaceName,
            registryClassName,
            contractTypeName,
            registryEntries);

        var hintPrefix = contract.Name;
        context.AddSource(
            $"{hintPrefix}.{keysClassName}.g.cs",
            SourceText.From(keysUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
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

    private sealed class StrategyRegistration
    {
        public StrategyRegistration(
            string key,
            INamedTypeSymbol contract,
            INamedTypeSymbol implementation,
            Location location)
        {
            Key = key;
            Contract = contract;
            Implementation = implementation;
            Location = location;
        }

        public string Key { get; }

        public INamedTypeSymbol Contract { get; }

        public INamedTypeSymbol Implementation { get; }

        public Location Location { get; }
    }
}
