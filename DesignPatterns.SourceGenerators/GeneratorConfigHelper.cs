using System;
using Microsoft.CodeAnalysis;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type

namespace DesignPatterns.SourceGenerators;

public readonly struct GeneratorIntegrationOptions
{
    public GeneratorIntegrationOptions(bool enableDi, bool enableAutofac)
    {
        EnableDi = enableDi;
        EnableAutofac = enableAutofac;
    }

    public bool EnableDi { get; }

    public bool EnableAutofac { get; }

    public bool NeedsRegistrationEntries => EnableDi || EnableAutofac;
}

internal static class GeneratorConfigHelper
{
    internal const string EnableDiIntegrationPropertyName = "build_property.DesignPatterns_EnableDiIntegration";
    internal const string EnableAutofacIntegrationPropertyName = "build_property.DesignPatterns_EnableAutofacIntegration";

    internal static IncrementalValueProvider<bool> CreateDiIntegrationEnabledProvider(
        IncrementalGeneratorInitializationContext context) =>
        context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(EnableDiIntegrationPropertyName, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

    internal static IncrementalValueProvider<bool> CreateAutofacIntegrationEnabledProvider(
        IncrementalGeneratorInitializationContext context) =>
        context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(EnableAutofacIntegrationPropertyName, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));

    internal static IncrementalValueProvider<GeneratorIntegrationOptions> CreateIntegrationOptionsProvider(
        IncrementalGeneratorInitializationContext context)
    {
        var diEnabled = CreateDiIntegrationEnabledProvider(context);
        var autofacEnabled = CreateAutofacIntegrationEnabledProvider(context);
        return diEnabled.Combine(autofacEnabled).Select(static (pair, _) =>
            new GeneratorIntegrationOptions(pair.Left, pair.Right));
    }
}
