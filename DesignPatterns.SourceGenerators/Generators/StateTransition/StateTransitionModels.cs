using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Generators.StateTransition;

/// <summary>
/// A single enum member (name + constant value).
/// </summary>
internal readonly record struct EnumMember(string Name, object? ConstantValue);

/// <summary>
/// Immutable, value-equatable enum type metadata extracted from a symbol.
/// </summary>
internal readonly record struct EnumTypeInfo(
    string Name,
    string? Namespace,
    string FullyQualifiedName,
    string FullyQualifiedDisplayString,
    EquatableArray<EnumMember> Members)
{
    public static EnumTypeInfo FromSymbol(INamedTypeSymbol symbol)
    {
        var members = symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.IsConst)
            .Select(static field => new EnumMember(field.Name, field.ConstantValue))
            .ToArray();

        return new EnumTypeInfo(
            symbol.Name,
            symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString(),
            symbol.ToDisplayString(),
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            new EquatableArray<EnumMember>(members));
    }
}

/// <summary>
/// Resolved initial state — <c>MemberName</c> is null when the value does
/// not match a member of the state enum.
/// </summary>
internal readonly record struct InitialStateInfo(string? MemberName, string DisplayValue, bool IsValid);

/// <summary>
/// Resolved transition argument (from / trigger / to). <c>MemberName</c>
/// is null when the value does not match a member of the expected enum.
/// </summary>
internal readonly record struct TransitionArg(
    string? MemberName,
    string DisplayValue,
    bool IsValid);

/// <summary>
/// Resolved guard method info extracted from the holder class symbol.
/// <c>MethodReference</c> is the fully-qualified delegate reference for
/// code emission (e.g. <c>global::Ns.Holder.CanSubmit</c>); it is
/// <c>null</c> when the guard is invalid or absent.
/// </summary>
internal readonly record struct GuardResolution(
    string Name,
    bool IsFound,
    bool IsStatic,
    bool HasValidSignature,
    string? MethodReference)
{
    /// <summary>
    /// <c>true</c> when the guard is absent (no Guard property on the
    /// attribute). An absent guard is always valid.
    /// </summary>
    public bool IsAbsent => Name is null;

    /// <summary>
    /// <c>true</c> when the guard is valid and should be emitted.
    /// </summary>
    public bool IsValid => IsAbsent || (IsFound && IsStatic && HasValidSignature);
}

/// <summary>
/// Resolved entry/exit action method info extracted from the holder class symbol.
/// Mirrors <see cref="GuardResolution"/> but adds an <c>IsAsync</c> flag to
/// distinguish sync (<c>void</c>) from async (<c>ValueTask</c>) action signatures.
/// <c>MethodReference</c> is the fully-qualified delegate reference for
/// code emission; it is <c>null</c> when the action is invalid or absent.
/// </summary>
internal readonly record struct ActionResolution(
    string Name,
    bool IsFound,
    bool IsStatic,
    bool HasValidSignature,
    bool IsAsync,
    string? MethodReference)
{
    /// <summary>
    /// <c>true</c> when the action is absent (no OnEnter/OnExit property on the
    /// attribute). An absent action is always valid.
    /// </summary>
    public bool IsAbsent => Name is null;

    /// <summary>
    /// <c>true</c> when the action is valid and should be emitted.
    /// </summary>
    public bool IsValid => IsAbsent || (IsFound && IsStatic && HasValidSignature);
}

/// <summary>
/// A raw transition extracted from a <c>[Transition]</c> attribute, before
/// validation / deduplication.
/// </summary>
internal sealed record TransitionModel(
    TransitionArg From,
    TransitionArg Trigger,
    TransitionArg To,
    Location Location,
    GuardResolution Guard,
    ActionResolution OnEnter,
    ActionResolution OnExit);

/// <summary>
/// A raw parent-child relationship extracted from a <c>[StateParent]</c>
/// attribute, before validation.
/// </summary>
internal sealed record StateParentModel(
    TransitionArg Child,
    TransitionArg Parent,
    Location Location);

/// <summary>
/// The full state machine model extracted from a class marked with
/// <c>[StateMachine]</c>.
/// </summary>
internal sealed record StateMachineModel(
    ContractInfo Holder,
    EnumTypeInfo? StateType,
    EnumTypeInfo? TriggerType,
    InitialStateInfo Initial,
    EquatableArray<TransitionModel> Transitions,
    EquatableArray<StateParentModel> StateParents,
    bool IsHierarchical,
    Location Location,
    bool IsValidHolder);

/// <summary>
/// Result of validating a <see cref="StateMachineModel"/>: the resolved
/// transitions (flattened if hierarchical) and the optional parent map
/// (child member name → parent member name) for hierarchy emission.
/// When hierarchical action chains are composed, <see cref="ActionChains"/>
/// carries the composite delegate definitions and per-edge override references.
/// </summary>
internal sealed record StateMachineValidationResult(
    List<ResolvedTransition> Transitions,
    Dictionary<string, string>? ParentMap,
    ActionChainResult? ActionChains);

/// <summary>
/// Per-state entry/exit action references collected from all declared edges.
/// In hierarchical mode, a state's exit action is the <c>OnExit</c> declared on
/// any edge where <c>from = state</c>; a state's enter action is the
/// <c>OnEnter</c> declared on any edge where <c>to = state</c>. When multiple
/// edges declare different actions for the same state, the first one encountered
/// wins (UML convention: a state has at most one entry and one exit action).
/// </summary>
internal sealed class StateActionMap
{
    public Dictionary<string, string> ExitSync { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> ExitAsync { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> EnterSync { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> EnterAsync { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Override action references for a single flattened edge. When the action
/// chain for an edge has more than one action, the override carries the
/// composite delegate method name; otherwise it carries the single original
/// reference (or <c>null</c> when the chain is empty).
/// </summary>
internal sealed record EdgeActionOverrides(
    string? OnEnterSyncReference,
    string? OnExitSyncReference,
    string? OnEnterAsyncReference,
    string? OnExitAsyncReference);

/// <summary>
/// A composite delegate definition to emit as a static method on the generated
/// transition table class. The method body calls each action reference in order.
/// </summary>
internal sealed record CompositeDelegateDefinition(
    string Name,
    bool IsAsync,
    bool IsExit,
    List<string> ActionReferences);

/// <summary>
/// The output of action chain composition: per-edge override references and
/// the composite delegate definitions to emit.
/// </summary>
internal sealed record ActionChainResult(
    Dictionary<(string From, string Trigger), EdgeActionOverrides> Overrides,
    List<CompositeDelegateDefinition> DelegateDefinitions);

/// <summary>
/// A transition that passed all per-edge validation and is ready for
/// code generation. Carries both the member names (for diagnostics) and
/// the formatted enum expressions (for code emission).
/// </summary>
internal sealed class ResolvedTransition
{
    public ResolvedTransition(
        string fromMember,
        string triggerMember,
        string toMember,
        Location location,
        string fromExpression,
        string triggerExpression,
        string toExpression,
        string? guardMethodReference,
        string? onEnterSyncReference,
        string? onExitSyncReference,
        string? onEnterAsyncReference,
        string? onExitAsyncReference)
    {
        FromMember = fromMember;
        TriggerMember = triggerMember;
        ToMember = toMember;
        Location = location;
        FromExpression = fromExpression;
        TriggerExpression = triggerExpression;
        ToExpression = toExpression;
        GuardMethodReference = guardMethodReference;
        OnEnterSyncReference = onEnterSyncReference;
        OnExitSyncReference = onExitSyncReference;
        OnEnterAsyncReference = onEnterAsyncReference;
        OnExitAsyncReference = onExitAsyncReference;
    }

    public string FromMember { get; }

    public string TriggerMember { get; }

    public string ToMember { get; }

    public Location Location { get; }

    public string FromExpression { get; }

    public string TriggerExpression { get; }

    public string ToExpression { get; }

    public string? GuardMethodReference { get; }

    public string? OnEnterSyncReference { get; }

    public string? OnExitSyncReference { get; }

    public string? OnEnterAsyncReference { get; }

    public string? OnExitAsyncReference { get; }

    /// <summary>
    /// Creates a copy of this transition with a different source state.
    /// Used by <see cref="HierarchyFlattener"/> to inherit parent-level edges.
    /// </summary>
    public ResolvedTransition WithFrom(
        string fromMember,
        string fromExpression)
        => new(
            fromMember,
            TriggerMember,
            ToMember,
            Location,
            fromExpression,
            TriggerExpression,
            ToExpression,
            GuardMethodReference,
            OnEnterSyncReference,
            OnExitSyncReference,
            OnEnterAsyncReference,
            OnExitAsyncReference);
}
