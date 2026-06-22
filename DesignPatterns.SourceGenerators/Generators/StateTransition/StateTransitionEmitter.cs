using System.Collections.Generic;
using System.Text;
using DesignPatterns.SourceGenerators.Generators;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators.StateTransition;

/// <summary>
/// Emission stage: builds and adds generated source for a validated
/// state machine (transition table + holder partial).
/// </summary>
internal static class StateTransitionEmitter
{
    /// <summary>
    /// Emits the transition table and holder partial for
    /// <paramref name="model"/> using the validated
    /// <paramref name="transitions"/>.
    /// </summary>
    public static void Emit(
        SourceProductionContext context,
        StateMachineModel model,
        List<ResolvedTransition> transitions)
    {
        var stateType = model.StateType!.Value;
        var triggerType = model.TriggerType!.Value;
        var initialMember = model.Initial.MemberName!;

        var tableClassName = StateTransitionSyntaxFactory.GetTransitionTableClassName(stateType.Name);
        var initialExpression = StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, initialMember);
        var transitionExpressions = transitions
            .ConvertAll(static transition => (transition.FromExpression, transition.TriggerExpression, transition.ToExpression));

        // Generated sources go into the holder's namespace (not the state enum's namespace)
        // so the holder partial merges with the user-written partial declaration.
        var namespaceName = model.Holder.Namespace;

        var tableUnit = StateTransitionSyntaxFactory.CreateTransitionTableCompilationUnit(
            namespaceName,
            tableClassName,
            stateType.FullyQualifiedDisplayString,
            triggerType.FullyQualifiedDisplayString,
            initialExpression,
            transitionExpressions);

        var holderUnit = StateTransitionSyntaxFactory.CreateHolderPartialCompilationUnit(
            namespaceName,
            model.Holder.Name,
            tableClassName,
            stateType.FullyQualifiedDisplayString,
            triggerType.FullyQualifiedDisplayString);

        var hintPrefix = HintNameHelper.FromString(stateType.FullyQualifiedDisplayString);
        context.AddSource(
            $"{hintPrefix}.{tableClassName}.g.cs",
            SourceText.From(tableUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{model.Holder.Name}.g.cs",
            SourceText.From(holderUnit.ToFullString(), Encoding.UTF8));
    }
}
