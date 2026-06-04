using System;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators;

internal static class GeneratorConfigHelper
{
    internal const string EnableDiIntegrationPropertyName = "build_property.DesignPatterns_EnableDiIntegration";

    internal static IncrementalValueProvider<bool> CreateDiIntegrationEnabledProvider(
        IncrementalGeneratorInitializationContext context) =>
        context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue(EnableDiIntegrationPropertyName, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
}
