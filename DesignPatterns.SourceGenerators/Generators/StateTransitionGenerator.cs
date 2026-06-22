using System.Collections.Immutable;
using DesignPatterns.SourceGenerators.Generators.StateTransition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates transition tables for classes marked with <c>[StateMachine]</c>.
/// </summary>
/// <remarks>
/// This class is the pipeline entry point only. Extraction, validation, and
/// emission are split into:
/// <list type="bullet">
/// <item><see cref="StateTransitionTransform"/> — attribute parsing → <see cref="StateMachineModel"/></item>
/// <item><see cref="StateTransitionValidator"/> — per-model / per-transition diagnostics (DP026–DP031)</item>
/// <item><see cref="StateTransitionEmitter"/> — transition table + holder partial emission</item>
/// </list>
/// This modular split mirrors the Vogen <c>GenerateCodeFor*.cs</c> pattern and
/// prepares for State v2 (guard / DI / EventAggregator linkage) where each
/// concern will get its own generator file.
/// </remarks>
[Generator]
public sealed class StateTransitionGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for <c>StateMachineAttribute</c>.</summary>
    public const string StateMachineMetadataName = "DesignPatterns.Behavioral.StateMachineAttribute";

    /// <summary>Metadata name for <c>TransitionAttribute</c>.</summary>
    public const string TransitionMetadataName = "DesignPatterns.Behavioral.TransitionAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var machines = context.SyntaxProvider.ForAttributeWithMetadataName(
            StateMachineMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => StateTransitionTransform.Transform(ctx))
            .WithTrackingName(TrackingNames.StateMachineTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            machines.Collect().Combine(integrationOptions),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<Result<StateMachineModel>> models,
        GeneratorIntegrationOptions integrationOptions)
    {
        foreach (var model in ResultExtensions.ReportAndCollect(context, models))
        {
            var transitions = StateTransitionValidator.Validate(context, model);
            if (transitions is not null)
            {
                StateTransitionEmitter.Emit(context, model, transitions, integrationOptions);
            }
        }
    }
}
