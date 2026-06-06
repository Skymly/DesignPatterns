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
/// Generates factory key constants and static registries for <c>[RegisterFactory]</c> implementations.
/// </summary>
[Generator]
public sealed class RegisterFactoryGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>RegisterFactoryAttribute</c>.</summary>
    public const string RegisterFactoryMetadataName = "DesignPatterns.Creational.RegisterFactoryAttribute";

    /// <summary>Metadata name for generic <c>RegisterFactoryAttribute&lt;TContract&gt;</c>.</summary>
    public const string RegisterFactoryGenericMetadataName = "DesignPatterns.Creational.RegisterFactoryAttribute`1";

    private static readonly RegistrationGeneratorDiagnostics Diagnostics = new(
        duplicateKey: new DiagnosticDescriptor(
            id: DiagnosticIds.RegisterFactoryDuplicateKey,
            title: "Duplicate factory key",
            messageFormat: "Factory key '{0}' is already registered for contract '{1}'",
            category: "DesignPatterns.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true),
        contractMismatch: new DiagnosticDescriptor(
            id: DiagnosticIds.RegisterFactoryContractMismatch,
            title: "Factory does not implement contract",
            messageFormat: "Type '{0}' does not implement factory contract '{1}'",
            category: "DesignPatterns.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true),
        missingParameterlessConstructor: new DiagnosticDescriptor(
            id: DiagnosticIds.RegisterFactoryMissingParameterlessConstructor,
            title: "Factory implementation requires a public parameterless constructor",
            messageFormat: "Type '{0}' must declare a public parameterless constructor to be used with generated static registration",
            category: "DesignPatterns.Generators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true));

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterFactoryMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => RegistrationGeneratorHelper.Transform(ctx, isGenericAttribute: false));

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterFactoryGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => RegistrationGeneratorHelper.Transform(ctx, isGenericAttribute: true));

        var diEnabled = GeneratorConfigHelper.CreateDiIntegrationEnabledProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()).Combine(diEnabled),
            static (spc, source) => RegistrationGeneratorHelper.Execute(
                spc,
                source.Left.Left.SelectMany(static list => list).ToImmutableArray(),
                source.Left.Right.SelectMany(static list => list).ToImmutableArray(),
                source.Right,
                Diagnostics,
                EmitGeneratedSources));
    }

    private static void EmitGeneratedSources(
        SourceProductionContext context,
        INamedTypeSymbol contract,
        List<KeyedRegistration> registrations,
        bool enableDiIntegration,
        bool qualifyHintName)
    {
        var namespaceName = contract.ContainingNamespace.IsGlobalNamespace
            ? null
            : contract.ContainingNamespace.ToDisplayString();

        var contractTypeName = contract.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var keysClassName = FactorySyntaxFactory.GetKeysClassName(contract);
        var registryClassName = FactorySyntaxFactory.GetRegistryClassName(contract);

        var constantNames = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<(string ConstantName, string KeyValue)>();
        foreach (var registration in registrations)
        {
            var constantName = FactorySyntaxFactory.ToConstantName(registration.Key);
            if (!constantNames.Add(constantName))
            {
                constantName += "_" + keys.Count;
            }

            keys.Add((constantName, registration.Key));
        }

        var registryEntries = registrations
            .Select(r => (r.Key, r.Implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToList();

        var keysUnit = FactorySyntaxFactory.CreateKeysCompilationUnit(namespaceName, keysClassName, keys);
        var registryUnit = FactorySyntaxFactory.CreateRegistryCompilationUnit(
            namespaceName,
            registryClassName,
            contractTypeName,
            registryEntries,
            enableDiIntegration);

        var hintPrefix = qualifyHintName ? HintNameHelper.FromSymbol(contract) : contract.Name;
        context.AddSource(
            $"{hintPrefix}.{keysClassName}.g.cs",
            SourceText.From(keysUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
    }
}
