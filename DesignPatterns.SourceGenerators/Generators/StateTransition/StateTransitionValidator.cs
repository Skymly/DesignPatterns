using System;
using System.Collections.Generic;
using System.Linq;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Generators;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;

namespace DesignPatterns.SourceGenerators.Generators.StateTransition;

/// <summary>
/// Validation stage: validates a <see cref="StateMachineModel"/> and reports
/// per-model / per-transition diagnostics (DP026–DP031). Returns the list of
/// valid, deduplicated <see cref="ResolvedTransition"/>s ready for emission,
/// or <c>null</c> when the model should be skipped (no emission).
/// </summary>
internal static class StateTransitionValidator
{
    /// <summary>
    /// Validates <paramref name="model"/>, reports diagnostics to
    /// <paramref name="context"/>, and returns the resolved transitions
    /// to emit. Returns <c>null</c> when no source should be generated
    /// for this model.
    /// </summary>
    public static List<ResolvedTransition>? Validate(
        SourceProductionContext context,
        StateMachineModel model)
    {
        if (!model.IsValidHolder)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateMachineHolderInvalid,
                model.Location,
                model.Holder.Name));
            return null;
        }

        if (model.StateType is null || model.TriggerType is null)
        {
            return null;
        }

        var stateType = model.StateType.Value;
        var triggerType = model.TriggerType.Value;

        if (!model.Initial.IsValid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionInvalidInitialState,
                model.Location,
                model.Initial.DisplayValue,
                stateType.FullyQualifiedName));
            return null;
        }

        var initialMember = model.Initial.MemberName!;
        var validTransitions = ResolveValidTransitions(context, model, stateType, triggerType);

        ReportDuplicateEdges(context, validTransitions);

        var uniqueTransitions = validTransitions
            .GroupBy(static transition => (transition.FromMember, transition.TriggerMember))
            .Select(static group => group.First())
            .ToList();

        ReportIsolatedStates(context, stateType, initialMember, uniqueTransitions, model.Location);

        if (uniqueTransitions.Count == 0 && model.Transitions.Count > 0)
        {
            return null;
        }

        return uniqueTransitions;
    }

    private static List<ResolvedTransition> ResolveValidTransitions(
        SourceProductionContext context,
        StateMachineModel model,
        EnumTypeInfo stateType,
        EnumTypeInfo triggerType)
    {
        var validTransitions = new List<ResolvedTransition>();
        foreach (var transition in model.Transitions)
        {
            if (!transition.From.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidStateMember,
                    transition.Location,
                    transition.From.DisplayValue,
                    stateType.FullyQualifiedName));
                continue;
            }

            if (!transition.To.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidStateMember,
                    transition.Location,
                    transition.To.DisplayValue,
                    stateType.FullyQualifiedName));
                continue;
            }

            if (!transition.Trigger.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidTriggerMember,
                    transition.Location,
                    transition.Trigger.DisplayValue,
                    triggerType.FullyQualifiedName));
                continue;
            }

            validTransitions.Add(new ResolvedTransition(
                transition.From.MemberName!,
                transition.Trigger.MemberName!,
                transition.To.MemberName!,
                transition.Location,
                StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, transition.From.MemberName!),
                StateTransitionSyntaxFactory.FormatEnumMember(triggerType.FullyQualifiedDisplayString, transition.Trigger.MemberName!),
                StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, transition.To.MemberName!)));
        }

        return validTransitions;
    }

    private static void ReportDuplicateEdges(
        SourceProductionContext context,
        List<ResolvedTransition> transitions)
    {
        foreach (var duplicateGroup in transitions
                     .GroupBy(static transition => (transition.FromMember, transition.TriggerMember))
                     .Where(static group => group.Count() > 1))
        {
            foreach (var transition in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionDuplicateEdge,
                    transition.Location,
                    transition.FromMember,
                    transition.TriggerMember));
            }
        }
    }

    private static void ReportIsolatedStates(
        SourceProductionContext context,
        EnumTypeInfo stateType,
        string initialMember,
        IReadOnlyCollection<ResolvedTransition> transitions,
        Location location)
    {
        var fromStates = new HashSet<string>(
            transitions.Select(static transition => transition.FromMember),
            StringComparer.Ordinal);

        foreach (var member in stateType.Members)
        {
            if (string.Equals(member.Name, initialMember, StringComparison.Ordinal))
            {
                continue;
            }

            if (!fromStates.Contains(member.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionIsolatedState,
                    location,
                    member.Name,
                    stateType.FullyQualifiedName));
            }
        }
    }
}
