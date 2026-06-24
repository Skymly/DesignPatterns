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

            if (!transition.Guard.IsValid)
            {
                ReportGuardDiagnostics(context, transition, model.Holder.Name, stateType, triggerType);
            }

            if (!transition.OnEnter.IsValid)
            {
                ReportActionDiagnostics(
                    context,
                    transition.Location,
                    transition.OnEnter,
                    model.Holder.Name,
                    stateType,
                    triggerType);
            }

            if (!transition.OnExit.IsValid)
            {
                ReportActionDiagnostics(
                    context,
                    transition.Location,
                    transition.OnExit,
                    model.Holder.Name,
                    stateType,
                    triggerType);
            }

            // Invalid guards/actions are reported as diagnostics but the transition is still
            // emitted without the broken delegate (reference is null). This is intentional:
            // the transition's state semantics are valid even when a delegate reference is
            // broken. The Error-level diagnostic forces the user to fix the method before
            // the build succeeds, at which point the delegate is re-emitted.
            validTransitions.Add(new ResolvedTransition(
                transition.From.MemberName!,
                transition.Trigger.MemberName!,
                transition.To.MemberName!,
                transition.Location,
                StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, transition.From.MemberName!),
                StateTransitionSyntaxFactory.FormatEnumMember(triggerType.FullyQualifiedDisplayString, transition.Trigger.MemberName!),
                StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, transition.To.MemberName!),
                transition.Guard.MethodReference,
                transition.OnEnter.IsAsync ? null : transition.OnEnter.MethodReference,
                transition.OnExit.IsAsync ? null : transition.OnExit.MethodReference,
                transition.OnEnter.IsAsync ? transition.OnEnter.MethodReference : null,
                transition.OnExit.IsAsync ? transition.OnExit.MethodReference : null));
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

    private static void ReportGuardDiagnostics(
        SourceProductionContext context,
        TransitionModel transition,
        string holderName,
        EnumTypeInfo stateType,
        EnumTypeInfo triggerType)
    {
        var guard = transition.Guard;

        if (!guard.IsFound)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionGuardMethodNotFound,
                transition.Location,
                guard.Name,
                holderName,
                stateType.FullyQualifiedName,
                triggerType.FullyQualifiedName));
            return;
        }

        if (!guard.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionGuardMethodNotStatic,
                transition.Location,
                guard.Name,
                holderName));
            return;
        }

        if (!guard.HasValidSignature)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionGuardMethodWrongSignature,
                transition.Location,
                guard.Name,
                holderName,
                stateType.FullyQualifiedName,
                triggerType.FullyQualifiedName));
        }
    }

    private static void ReportActionDiagnostics(
        SourceProductionContext context,
        Location location,
        ActionResolution action,
        string holderName,
        EnumTypeInfo stateType,
        EnumTypeInfo triggerType)
    {
        if (!action.IsFound)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionActionMethodNotFound,
                location,
                action.Name,
                holderName,
                stateType.FullyQualifiedName,
                triggerType.FullyQualifiedName));
            return;
        }

        if (!action.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionActionMethodNotStatic,
                location,
                action.Name,
                holderName));
            return;
        }

        if (!action.HasValidSignature)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionActionMethodWrongSignature,
                location,
                action.Name,
                holderName,
                stateType.FullyQualifiedName,
                triggerType.FullyQualifiedName));
        }
    }
}
