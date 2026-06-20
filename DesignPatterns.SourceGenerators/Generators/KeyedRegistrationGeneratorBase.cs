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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Abstraction over the syntax factory methods that build keys and registry
/// compilation units. Implemented by <see cref="FactorySyntaxFactoryAdapter"/>
/// and <see cref="StrategySyntaxFactoryAdapter"/>.
/// </summary>
public interface IKeyedRegistrationSyntaxFactory
{
    CompilationUnitSyntax CreateKeysCompilationUnit(
        string? namespaceName,
        string keysClassName,
        IReadOnlyList<(string ConstantName, string KeyValue)> keys);

    CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries,
        GeneratorIntegrationOptions integrationOptions = default);

    string GetKeysClassName(string contractName);

    string GetRegistryClassName(string contractName);

    string ToConstantName(string key);
}

/// <summary>
/// Base class for keyed-registration incremental generators (factory, strategy).
/// Subclasses provide attribute metadata names, diagnostics, tracking names,
/// and the <see cref="IKeyedRegistrationSyntaxFactory"/> implementation that
/// generates keys/registry syntax. All pipeline wiring is shared.
/// </summary>
public abstract class KeyedRegistrationGeneratorBase : IIncrementalGenerator
{
    /// <summary>Metadata name for the non-generic registration attribute.</summary>
    protected abstract string NonGenericMetadataName { get; }

    /// <summary>Metadata name for the generic registration attribute.</summary>
    protected abstract string GenericMetadataName { get; }

    /// <summary>Tracking name for the non-generic transform stage.</summary>
    protected abstract string NonGenericTrackingName { get; }

    /// <summary>Tracking name for the generic transform stage.</summary>
    protected abstract string GenericTrackingName { get; }

    /// <summary>Diagnostic descriptors for this generator.</summary>
    protected abstract KeyedRegistrationDiagnostics Diagnostics { get; }

    /// <summary>Syntax factory that builds keys and registry compilation units.</summary>
    protected abstract IKeyedRegistrationSyntaxFactory SyntaxFactory { get; }

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGenericMetadataName = NonGenericMetadataName;
        var genericMetadataName = GenericMetadataName;
        var nonGenericTrackingName = NonGenericTrackingName;
        var genericTrackingName = GenericTrackingName;

        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            nonGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => RegistrationGeneratorHelper.Transform(ctx, isGenericAttribute: false))
            .WithTrackingName(nonGenericTrackingName);

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            genericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => RegistrationGeneratorHelper.Transform(ctx, isGenericAttribute: true))
            .WithTrackingName(genericTrackingName);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        var diagnostics = Diagnostics;
        var syntaxFactory = SyntaxFactory;

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()).Combine(integrationOptions),
            (spc, source) => RegistrationGeneratorHelper.Execute(
                spc,
                source.Left.Left.SelectMany(static list => list).ToImmutableArray(),
                source.Left.Right.SelectMany(static list => list).ToImmutableArray(),
                source.Right,
                diagnostics,
                (context2, contract, registrations, options, qualifyHintName) =>
                    EmitGeneratedSources(context2, contract, registrations, options, qualifyHintName, syntaxFactory)));
    }

    private static void EmitGeneratedSources(
        SourceProductionContext context,
        ContractInfo contract,
        List<KeyedRegistration> registrations,
        GeneratorIntegrationOptions integrationOptions,
        bool qualifyHintName,
        IKeyedRegistrationSyntaxFactory syntaxFactory)
    {
        var keysClassName = syntaxFactory.GetKeysClassName(contract.Name);
        var registryClassName = syntaxFactory.GetRegistryClassName(contract.Name);

        var constantNames = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<(string ConstantName, string KeyValue)>();
        foreach (var registration in registrations)
        {
            var constantName = syntaxFactory.ToConstantName(registration.Key);
            if (!constantNames.Add(constantName))
            {
                constantName += "_" + keys.Count;
            }

            keys.Add((constantName, registration.Key));
        }

        var registryEntries = registrations
            .Select(static r => (r.Key, r.ImplementationFullyQualifiedDisplayString))
            .ToList();

        var keysUnit = syntaxFactory.CreateKeysCompilationUnit(contract.Namespace, keysClassName, keys);
        var registryUnit = syntaxFactory.CreateRegistryCompilationUnit(
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

/// <summary>
/// Adapter that exposes <see cref="FactorySyntaxFactory"/> through
/// <see cref="IKeyedRegistrationSyntaxFactory"/>.
/// </summary>
public sealed class FactorySyntaxFactoryAdapter : IKeyedRegistrationSyntaxFactory
{
    public CompilationUnitSyntax CreateKeysCompilationUnit(
        string? namespaceName,
        string keysClassName,
        IReadOnlyList<(string ConstantName, string KeyValue)> keys) =>
        FactorySyntaxFactory.CreateKeysCompilationUnit(namespaceName, keysClassName, keys);

    public CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        FactorySyntaxFactory.CreateRegistryCompilationUnit(
            namespaceName, registryClassName, contractTypeName, entries, integrationOptions);

    public string GetKeysClassName(string contractName) =>
        FactorySyntaxFactory.GetKeysClassName(contractName);

    public string GetRegistryClassName(string contractName) =>
        FactorySyntaxFactory.GetRegistryClassName(contractName);

    public string ToConstantName(string key) =>
        FactorySyntaxFactory.ToConstantName(key);
}

/// <summary>
/// Adapter that exposes <see cref="StrategySyntaxFactory"/> through
/// <see cref="IKeyedRegistrationSyntaxFactory"/>.
/// </summary>
public sealed class StrategySyntaxFactoryAdapter : IKeyedRegistrationSyntaxFactory
{
    public CompilationUnitSyntax CreateKeysCompilationUnit(
        string? namespaceName,
        string keysClassName,
        IReadOnlyList<(string ConstantName, string KeyValue)> keys) =>
        StrategySyntaxFactory.CreateKeysCompilationUnit(namespaceName, keysClassName, keys);

    public CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        StrategySyntaxFactory.CreateRegistryCompilationUnit(
            namespaceName, registryClassName, contractTypeName, entries, integrationOptions);

    public string GetKeysClassName(string contractName) =>
        StrategySyntaxFactory.GetKeysClassName(contractName);

    public string GetRegistryClassName(string contractName) =>
        StrategySyntaxFactory.GetRegistryClassName(contractName);

    public string ToConstantName(string key) =>
        StrategySyntaxFactory.ToConstantName(key);
}
