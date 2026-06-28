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
/// per-model / per-transition diagnostics (DP026–DP031, DP056–DP059). Returns
/// the validated (and flattened if hierarchical) <see cref="ResolvedTransition"/>s
/// plus optional parent map, or <c>null</c> when the model should be skipped.
/// </summary>
internal static class StateTransitionValidator
{
    /// <summary>
    /// Validates <paramref name="model"/>, reports diagnostics to
    /// <paramref name="context"/>, and returns the resolved transitions
    /// and optional parent map to emit. Returns <c>null</c> when no source
    /// should be generated for this model.
    /// </summary>
    public static StateMachineValidationResult? Validate(
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

        // Validate and build hierarchy if hierarchical mode is enabled.
        Dictionary<string, string>? parentMap = null;
        if (model.IsHierarchical)
        {
            parentMap = ValidateStateParents(context, model, stateType);
            if (parentMap is not null)
            {
                // Flatten inherited transitions.
                uniqueTransitions = HierarchyFlattener.Flatten(uniqueTransitions, parentMap);

                // Re-deduplicate after flattening (inherited edges may duplicate).
                uniqueTransitions = uniqueTransitions
                    .GroupBy(static t => (t.FromMember, t.TriggerMember))
                    .Select(static g => g.First())
                    .ToList();

                // Report orphan parents (DP059).
                ReportOrphanParents(context, stateType, parentMap, uniqueTransitions, model.Location);
            }
        }

        ReportIsolatedStates(context, stateType, initialMember, uniqueTransitions, model.Location);

        if (uniqueTransitions.Count == 0 && model.Transitions.Count > 0)
        {
            return null;
        }

        return new StateMachineValidationResult(uniqueTransitions, parentMap);
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

    // --- State hierarchy validation (DP056–DP059) ---

    /// <summary>
    /// Validates [StateParent] declarations and builds the parent map.
    /// Returns <c>null</c> when a fatal error (cycle, invalid member) prevents
    /// hierarchy construction; the caller should skip flattening but may still
    /// emit the flat table.
    /// </summary>
    private static Dictionary<string, string>? ValidateStateParents(
        SourceProductionContext context,
        StateMachineModel model,
        EnumTypeInfo stateType)
    {
        var validMembers = new HashSet<string>(
            stateType.Members.Select(static m => m.Name),
            StringComparer.Ordinal);

        var parentMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasFatalError = false;

        foreach (var sp in model.StateParents)
        {
            // DP057: Invalid enum member
            if (!sp.Child.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateParentInvalidMember,
                    sp.Location,
                    sp.Child.DisplayValue,
                    stateType.FullyQualifiedName));
                hasFatalError = true;
                continue;
            }

            if (!sp.Parent.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateParentInvalidMember,
                    sp.Location,
                    sp.Parent.DisplayValue,
                    stateType.FullyQualifiedName));
                hasFatalError = true;
                continue;
            }

            var childName = sp.Child.MemberName!;
            var parentName = sp.Parent.MemberName!;

            // DP058: Self-reference
            if (string.Equals(childName, parentName, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateParentSelfReference,
                    sp.Location,
                    childName));
                hasFatalError = true;
                continue;
            }

            // Multiple inheritance check (single inheritance only)
            if (parentMap.TryGetValue(childName, out var existingParent)
                && !string.Equals(existingParent, parentName, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateParentInvalidMember,
                    sp.Location,
                    $"{childName} already has parent '{existingParent}'",
                    stateType.FullyQualifiedName));
                hasFatalError = true;
                continue;
            }

            parentMap[childName] = parentName;
        }

        // DP056: Cycle detection
        if (parentMap.Count > 0)
        {
            var cycle = DetectCycle(parentMap);
            if (cycle is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateHierarchyCycle,
                    model.Location,
                    cycle));
                hasFatalError = true;
            }
        }

        return hasFatalError ? null : parentMap;
    }

    /// <summary>
    /// Detects a cycle in the parent map. Returns a string describing the cycle
    /// (e.g. "A -> B -> C -> A") or <c>null</c> if no cycle exists.
    /// </summary>
    private static string? DetectCycle(Dictionary<string, string> parentMap)
    {
        foreach (var start in parentMap.Keys)
        {
            var visited = new List<string>();
            var current = start;
            var seen = new HashSet<string>();

            while (current is not null)
            {
                if (!seen.Add(current))
                {
                    // Found a cycle — build the description.
                    var cycleStart = current;
                    var cyclePath = new List<string> { cycleStart };
                    var node = parentMap[cycleStart];
                    while (!string.Equals(node, cycleStart, StringComparison.Ordinal))
                    {
                        cyclePath.Add(node);
                        node = parentMap[node];
                    }

                    cyclePath.Add(cycleStart); // Close the loop
                    return string.Join(" -> ", cyclePath);
                }

                visited.Add(current);
                if (!parentMap.TryGetValue(current, out var parent))
                {
                    break; // Reached root — no cycle from this start.
                }

                current = parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Reports DP059 (Info): parent states that have no outgoing transitions.
    /// This is a hint that the parent state only serves as a grouping container
    /// and could potentially be simplified.
    /// </summary>
    private static void ReportOrphanParents(
        SourceProductionContext context,
        EnumTypeInfo stateType,
        Dictionary<string, string> parentMap,
        IReadOnlyCollection<ResolvedTransition> transitions,
        Location location)
    {
        var parentStates = new HashSet<string>(
            parentMap.Values,
            StringComparer.Ordinal);

        var statesWithEdges = new HashSet<string>(
            transitions.Select(static t => t.FromMember),
            StringComparer.Ordinal);

        foreach (var parentState in parentStates)
        {
            if (!statesWithEdges.Contains(parentState))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateParentOrphanParent,
                    location,
                    parentState));
            }
        }
    }
}
