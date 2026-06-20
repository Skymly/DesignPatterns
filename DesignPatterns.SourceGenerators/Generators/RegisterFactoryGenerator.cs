using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates factory key constants and static registries for <c>[RegisterFactory]</c> implementations.
/// </summary>
[Generator]
public sealed class RegisterFactoryGenerator : KeyedRegistrationGeneratorBase
{
    /// <summary>Metadata name for non-generic <c>RegisterFactoryAttribute</c>.</summary>
    public const string RegisterFactoryMetadataName = "DesignPatterns.Creational.RegisterFactoryAttribute";

    /// <summary>Metadata name for generic <c>RegisterFactoryAttribute&lt;TContract&gt;</c>.</summary>
    public const string RegisterFactoryGenericMetadataName = "DesignPatterns.Creational.RegisterFactoryAttribute`1";

    private static readonly KeyedRegistrationDiagnostics DiagnosticsField =
        DesignPatternsDiagnosticDescriptors.RegisterFactory;

    private static readonly IKeyedRegistrationSyntaxFactory SyntaxFactoryField =
        new FactorySyntaxFactoryAdapter();

    /// <inheritdoc />
    protected override string NonGenericMetadataName => RegisterFactoryMetadataName;

    /// <inheritdoc />
    protected override string GenericMetadataName => RegisterFactoryGenericMetadataName;

    /// <inheritdoc />
    protected override string NonGenericTrackingName => TrackingNames.FactoryNonGenericTransform;

    /// <inheritdoc />
    protected override string GenericTrackingName => TrackingNames.FactoryGenericTransform;

    /// <inheritdoc />
    protected override KeyedRegistrationDiagnostics Diagnostics => DiagnosticsField;

    /// <inheritdoc />
    protected override IKeyedRegistrationSyntaxFactory SyntaxFactory => SyntaxFactoryField;
}
