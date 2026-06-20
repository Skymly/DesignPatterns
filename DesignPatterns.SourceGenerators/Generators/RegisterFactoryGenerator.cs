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

    private static readonly KeyedRegistrationDiagnostics Diagnostics =
        DesignPatternsDiagnosticDescriptors.RegisterFactory;

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
        ContractInfo contract,
        List<KeyedRegistration> registrations,
        GeneratorIntegrationOptions integrationOptions,
        bool qualifyHintName)
    {
        var keysClassName = FactorySyntaxFactory.GetKeysClassName(contract.Name);
        var registryClassName = FactorySyntaxFactory.GetRegistryClassName(contract.Name);

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
            .Select(static r => (r.Key, r.ImplementationFullyQualifiedDisplayString))
            .ToList();

        var keysUnit = FactorySyntaxFactory.CreateKeysCompilationUnit(contract.Namespace, keysClassName, keys);
        var registryUnit = FactorySyntaxFactory.CreateRegistryCompilationUnit(
            contract.Namespace,
            registryClassName,
            contract.FullyQualifiedDisplayString,
            registryEntries,
            integrationOptions);

        var hintPrefix = qualifyHintName ? HintNameHelper.FromString(contract.FullyQualifiedDisplayString) : contract.Name;
        context.AddSource(
            $"{hintPrefix}.{keysClassName}.g.cs",
            SourceText.From(keysUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{registryClassName}.g.cs",
            SourceText.From(registryUnit.ToFullString(), Encoding.UTF8));
    }
}
