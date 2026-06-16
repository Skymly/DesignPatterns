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

    private static readonly KeyedRegistrationDiagnostics Diagnostics =
        DesignPatternsDiagnosticDescriptors.RegisterStrategy;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterStrategyMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => RegistrationGeneratorHelper.Transform(ctx, isGenericAttribute: false));

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            RegisterStrategyGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => RegistrationGeneratorHelper.Transform(ctx, isGenericAttribute: true));

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()).Combine(integrationOptions),
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
        GeneratorIntegrationOptions integrationOptions,
        bool qualifyHintName)
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
            registryEntries,
            integrationOptions);

        var hintPrefix = qualifyHintName ? HintNameHelper.FromSymbol(contract) : contract.Name;
        context.AddSource(
            $"{hintPrefix}.{keysClassName}.g.cs",
            SourceText.From(keysUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
    }
}
