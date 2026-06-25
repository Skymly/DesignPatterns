using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates strategy key constants and static registries for <c>[RegisterStrategy]</c> implementations.
/// </summary>
[Generator]
public sealed class RegisterStrategyGenerator : KeyedRegistrationGeneratorBase
{
    /// <summary>Metadata name for non-generic <c>RegisterStrategyAttribute</c>.</summary>
    public const string RegisterStrategyMetadataName = "DesignPatterns.Behavioral.RegisterStrategyAttribute";

    /// <summary>Metadata name for generic <c>RegisterStrategyAttribute&lt;TContract&gt;</c>.</summary>
    public const string RegisterStrategyGenericMetadataName = "DesignPatterns.Behavioral.RegisterStrategyAttribute`1";

    private static readonly KeyedRegistrationDiagnostics DiagnosticsField =
        DesignPatternsDiagnosticDescriptors.RegisterStrategy;

    private static readonly IKeyedRegistrationSyntaxFactory SyntaxFactoryField =
        new StrategySyntaxFactoryAdapter();

    /// <inheritdoc />
    protected override string NonGenericMetadataName => RegisterStrategyMetadataName;

    /// <inheritdoc />
    protected override string GenericMetadataName => RegisterStrategyGenericMetadataName;

    /// <inheritdoc />
    protected override string NonGenericTrackingName => TrackingNames.StrategyNonGenericTransform;

    /// <inheritdoc />
    protected override string GenericTrackingName => TrackingNames.StrategyGenericTransform;

    /// <inheritdoc />
    protected override string CombineTrackingName => TrackingNames.StrategyCombine;

    /// <inheritdoc />
    protected override KeyedRegistrationDiagnostics Diagnostics => DiagnosticsField;

    /// <inheritdoc />
    protected override IKeyedRegistrationSyntaxFactory SyntaxFactory => SyntaxFactoryField;
}
