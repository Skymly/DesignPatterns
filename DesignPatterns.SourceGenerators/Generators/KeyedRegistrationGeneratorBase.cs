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
        IReadOnlyList<(string Key, string ImplementationTypeName, string? GuardMethodReference)> entries,
        GeneratorIntegrationOptions integrationOptions = default);

    CompilationUnitSyntax? CreateAsyncRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default);

    CompilationUnitSyntax? CreatePooledRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        int poolSize,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default);

    string GetKeysClassName(string contractName);

    string GetRegistryClassName(string contractName);

    string GetAsyncRegistryClassName(string contractName);

    string GetPooledRegistryClassName(string contractName);

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

    /// <summary>Tracking name for the Combine stage.</summary>
    protected abstract string CombineTrackingName { get; }

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
        var combineTrackingName = CombineTrackingName;

        context.RegisterSourceOutput(
            nonGeneric.Collect()
                .Combine(generic.Collect())
                .WithTrackingName(combineTrackingName)
                .Combine(integrationOptions)
                .WithTrackingName(combineTrackingName),
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
            .Select(static r => (r.Key, r.ImplementationFullyQualifiedDisplayString, r.Guard.MethodReference))
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

        // Emit async registry when any registration is async (IsAsync=true or implements IAsyncFactory<T>).
        var asyncEntries = registrations
            .Where(static r => r.IsAsync || r.ImplementsAsyncFactory)
            .Select(static r => (r.Key, r.ImplementationFullyQualifiedDisplayString, r.ImplementsAsyncFactory))
            .ToList();

        if (asyncEntries.Count > 0)
        {
            var asyncRegistryClassName = syntaxFactory.GetAsyncRegistryClassName(contract.Name);
            var asyncRegistryUnit = syntaxFactory.CreateAsyncRegistryCompilationUnit(
                contract.Namespace,
                asyncRegistryClassName,
                contract.FullyQualifiedDisplayString,
                asyncEntries,
                integrationOptions);

            if (asyncRegistryUnit is not null)
            {
                context.AddSource(
                    $"{hintPrefix}.{asyncRegistryClassName}.g.cs",
                    SourceText.From(asyncRegistryUnit.ToFullString(), Encoding.UTF8));
            }
        }

        // Emit pooled registry when any registration has PoolSize > 0.
        var pooledEntries = registrations
            .Where(static r => r.PoolSize > 0 && (r.IsAsync || r.ImplementsAsyncFactory))
            .Select(static r => (r.Key, r.ImplementationFullyQualifiedDisplayString, r.ImplementsAsyncFactory))
            .ToList();

        if (pooledEntries.Count > 0)
        {
            var maxPoolSize = registrations.Where(static r => r.PoolSize > 0).Max(static r => r.PoolSize);
            var pooledRegistryClassName = syntaxFactory.GetPooledRegistryClassName(contract.Name);
            var pooledRegistryUnit = syntaxFactory.CreatePooledRegistryCompilationUnit(
                contract.Namespace,
                pooledRegistryClassName,
                contract.FullyQualifiedDisplayString,
                maxPoolSize,
                pooledEntries,
                integrationOptions);

            if (pooledRegistryUnit is not null)
            {
                context.AddSource(
                    $"{hintPrefix}.{pooledRegistryClassName}.g.cs",
                    SourceText.From(pooledRegistryUnit.ToFullString(), Encoding.UTF8));
            }
        }
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
        IReadOnlyList<(string Key, string ImplementationTypeName, string? GuardMethodReference)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        FactorySyntaxFactory.CreateRegistryCompilationUnit(
            namespaceName, registryClassName, contractTypeName, entries, integrationOptions);

    public CompilationUnitSyntax? CreateAsyncRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        FactorySyntaxFactory.CreateAsyncRegistryCompilationUnit(
            namespaceName, registryClassName, contractTypeName, entries, integrationOptions);

    public CompilationUnitSyntax? CreatePooledRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        int poolSize,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        FactorySyntaxFactory.CreatePooledRegistryCompilationUnit(
            namespaceName, registryClassName, contractTypeName, poolSize, entries, integrationOptions);

    public string GetKeysClassName(string contractName) =>
        FactorySyntaxFactory.GetKeysClassName(contractName);

    public string GetRegistryClassName(string contractName) =>
        FactorySyntaxFactory.GetRegistryClassName(contractName);

    public string GetAsyncRegistryClassName(string contractName) =>
        FactorySyntaxFactory.GetAsyncRegistryClassName(contractName);

    public string GetPooledRegistryClassName(string contractName) =>
        FactorySyntaxFactory.GetPooledRegistryClassName(contractName);

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
        IReadOnlyList<(string Key, string ImplementationTypeName, string? GuardMethodReference)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        StrategySyntaxFactory.CreateRegistryCompilationUnit(
            namespaceName, registryClassName, contractTypeName, entries, integrationOptions);

    public string GetKeysClassName(string contractName) =>
        StrategySyntaxFactory.GetKeysClassName(contractName);

    public string GetRegistryClassName(string contractName) =>
        StrategySyntaxFactory.GetRegistryClassName(contractName);

    public CompilationUnitSyntax? CreateAsyncRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        null;

    public CompilationUnitSyntax? CreatePooledRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        int poolSize,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default) =>
        null;

    public string GetAsyncRegistryClassName(string contractName) =>
        StrategySyntaxFactory.GetRegistryClassName(contractName);

    public string GetPooledRegistryClassName(string contractName) =>
        StrategySyntaxFactory.GetRegistryClassName(contractName);

    public string ToConstantName(string key) =>
        StrategySyntaxFactory.ToConstantName(key);
}
