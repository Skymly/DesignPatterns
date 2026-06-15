#pragma warning disable CS1591 // Public descriptor registry; per-property XML docs add noise without IDE value.

using Microsoft.CodeAnalysis;

namespace DesignPatterns.Diagnostics;

/// <summary>
/// Central <see cref="DiagnosticDescriptor"/> definitions for DesignPatterns generators and analyzers.
/// </summary>
public static class DesignPatternsDiagnosticDescriptors
{
    private const string DiagnosticsPage = "https://skymly.github.io/DesignPatterns.Docs/diagnostics";
    private const string GeneratorCategory = "DesignPatterns.Generators";
    private const string AnalyzerCategory = "DesignPatterns.Analyzers";

    // Singleton (DP001–DP002)

    public static DiagnosticDescriptor GenerateSingletonNotPartial { get; } = Create(
        DiagnosticIds.GenerateSingletonNotPartial,
        "GenerateSingleton requires a partial class",
        "Class '{0}' must be declared partial so [GenerateSingleton] can emit Instance members. Add the partial keyword to the class declaration.",
        "GenerateSingleton augments partial classes with generated lazy singleton members.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor GenerateSingletonInvalidTarget { get; } = Create(
        DiagnosticIds.GenerateSingletonInvalidTarget,
        "GenerateSingleton target is invalid",
        "[GenerateSingleton] cannot be applied to '{0}'. Apply it only to a non-static, non-generic class.",
        "GenerateSingleton is limited to eligible class declarations.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    // Strategy (DP003–DP004, DP007)

    public static KeyedRegistrationDiagnostics RegisterStrategy { get; } = new(
        Create(
            DiagnosticIds.RegisterStrategyDuplicateKey,
            "Duplicate strategy key",
            "Strategy key '{0}' is already registered for contract '{1}'. Use a unique key or remove the duplicate [RegisterStrategy] attribute.",
            "Each strategy key must be unique within a contract registry.",
            DiagnosticSeverity.Error,
            GeneratorCategory),
        Create(
            DiagnosticIds.RegisterStrategyContractMismatch,
            "Strategy does not implement contract",
            "Type '{0}' does not implement strategy contract '{1}'. Implement the contract or fix the [RegisterStrategy] contract argument.",
            "RegisterStrategy implementations must implement the declared contract type.",
            DiagnosticSeverity.Error,
            GeneratorCategory),
        Create(
            DiagnosticIds.RegisterStrategyMissingParameterlessConstructor,
            "Strategy requires a public parameterless constructor",
            "Type '{0}' must declare a public parameterless constructor for [RegisterStrategy] static registration, or enable DI integration.",
            "Static strategy registries instantiate implementations with a parameterless constructor.",
            DiagnosticSeverity.Error,
            GeneratorCategory));

    public static DiagnosticDescriptor RegisterStrategyUnregisteredImplementation { get; } = Create(
        DiagnosticIds.RegisterStrategyUnregisteredImplementation,
        "Strategy implementation is not registered",
        "Type '{0}' implements strategy contract '{1}' but has no [RegisterStrategy] attribute. Add [RegisterStrategy(\"key\", typeof(...))] with a unique key and the contract type to register it.",
        "Concrete strategy types should be registered when a contract registry is in use.",
        DiagnosticSeverity.Info,
        AnalyzerCategory);

    // Chain (DP005, DP008–DP009, DP024)

    public static DiagnosticDescriptor HandlerOrderDuplicateOrder { get; } = Create(
        DiagnosticIds.HandlerOrderDuplicateOrder,
        "Duplicate handler order",
        "Handler order '{0}' is already used for context '{1}'. Assign a unique order value or remove the duplicate [HandlerOrder] attribute.",
        "Handler pipeline order values must be unique per context type.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor HandlerOrderContractMismatch { get; } = Create(
        DiagnosticIds.HandlerOrderContractMismatch,
        "Handler does not implement IHandler for context",
        "Type '{0}' does not implement IHandler<{1}>. Implement IHandler<{1}> or fix the [HandlerOrder] context argument.",
        "HandlerOrder requires the handler type to implement IHandler<TContext>.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor HandlerOrderMissingParameterlessConstructor { get; } = Create(
        DiagnosticIds.HandlerOrderMissingParameterlessConstructor,
        "Handler requires a public parameterless constructor",
        "Type '{0}' must declare a public parameterless constructor for generated handler pipelines, or enable DI integration.",
        "Static handler pipelines instantiate handlers with a parameterless constructor.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor HandlerOrderUnregisteredImplementation { get; } = Create(
        DiagnosticIds.HandlerOrderUnregisteredImplementation,
        "Handler implementation is not registered",
        "Type '{0}' implements IHandler<{1}> but has no [HandlerOrder] attribute. Add [HandlerOrder(order, typeof(...))] with a unique order and the context type to include it in the pipeline.",
        "Concrete handler types should declare HandlerOrder when a pipeline context is registered.",
        DiagnosticSeverity.Info,
        AnalyzerCategory);

    // Composite (DP010–DP015)

    public static DiagnosticDescriptor CompositePartDuplicateKey { get; } = Create(
        DiagnosticIds.CompositePartDuplicateKey,
        "Duplicate composite key",
        "Composite key '{0}' is already registered for contract '{1}'. Use a unique key or remove the duplicate [CompositePart] attribute.",
        "Composite part keys must be unique within a contract catalog.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor CompositePartUnknownParentKey { get; } = Create(
        DiagnosticIds.CompositePartUnknownParentKey,
        "Unknown composite parent key",
        "Composite parent key '{0}' was not found for contract '{1}'. Register the parent part first or correct the ParentKey value.",
        "CompositePart ParentKey must reference another registered part key.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor CompositePartCycle { get; } = Create(
        DiagnosticIds.CompositePartCycle,
        "Composite parent chain cycle",
        "Composite key '{0}' participates in a parent-key cycle for contract '{1}'. Remove or reassign ParentKey values to break the cycle.",
        "Composite catalogs cannot contain cyclic parent relationships.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor CompositePartContractMismatch { get; } = Create(
        DiagnosticIds.CompositePartContractMismatch,
        "Composite part does not implement contract",
        "Type '{0}' does not implement composite contract '{1}'. Implement the contract or fix the [CompositePart] contract argument.",
        "CompositePart implementations must implement the declared contract type.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor CompositePartMissingParameterlessConstructor { get; } = Create(
        DiagnosticIds.CompositePartMissingParameterlessConstructor,
        "Composite part requires a public parameterless constructor",
        "Type '{0}' must declare a public parameterless constructor for generated composite catalogs.",
        "Composite catalogs instantiate parts with a parameterless constructor.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor CompositePartMissingBuildable { get; } = Create(
        DiagnosticIds.CompositePartMissingBuildable,
        "Composite part must implement ICompositeBuildable",
        "Type '{0}' must implement ICompositeBuildable<{1}> to be used with generated composite catalogs.",
        "Composite catalogs require parts to expose a Build() factory via ICompositeBuildable<T>.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    // Decorator (DP016–DP019)

    public static DiagnosticDescriptor DecoratorDuplicateOrder { get; } = Create(
        DiagnosticIds.DecoratorDuplicateOrder,
        "Duplicate decorator order",
        "Decorator order '{0}' is already used for service contract '{1}'. Assign a unique order value or remove the duplicate [Decorator] attribute.",
        "Decorator order values must be unique per service contract.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor DecoratorContractMismatch { get; } = Create(
        DiagnosticIds.DecoratorContractMismatch,
        "Decorator does not implement service contract",
        "Type '{0}' does not implement service contract '{1}'. Implement the contract or fix the [Decorator] service argument.",
        "Decorators must implement the decorated service contract.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor DecoratorMissingDecoratorInterface { get; } = Create(
        DiagnosticIds.DecoratorMissingDecoratorInterface,
        "Decorator does not implement IDecorator",
        "Type '{0}' does not implement IDecorator<{1}>. Implement IDecorator<{1}> to participate in generated decorator stacks.",
        "Generated decorator stacks require IDecorator<TService> implementations.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor DecoratorMissingParameterlessConstructor { get; } = Create(
        DiagnosticIds.DecoratorMissingParameterlessConstructor,
        "Decorator requires a public parameterless constructor",
        "Type '{0}' must declare a public parameterless constructor for generated decorator stacks.",
        "Decorator stacks instantiate decorators with a parameterless constructor.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    // Factory (DP020–DP022, DP023)

    public static KeyedRegistrationDiagnostics RegisterFactory { get; } = new(
        Create(
            DiagnosticIds.RegisterFactoryDuplicateKey,
            "Duplicate factory key",
            "Factory key '{0}' is already registered for contract '{1}'. Use a unique key or remove the duplicate [RegisterFactory] attribute.",
            "Each factory key must be unique within a contract registry.",
            DiagnosticSeverity.Error,
            GeneratorCategory),
        Create(
            DiagnosticIds.RegisterFactoryContractMismatch,
            "Factory does not implement contract",
            "Type '{0}' does not implement factory contract '{1}'. Implement the contract or fix the [RegisterFactory] contract argument.",
            "RegisterFactory implementations must implement the declared contract type.",
            DiagnosticSeverity.Error,
            GeneratorCategory),
        Create(
            DiagnosticIds.RegisterFactoryMissingParameterlessConstructor,
            "Factory requires a public parameterless constructor",
            "Type '{0}' must declare a public parameterless constructor for [RegisterFactory] static registration, or enable DI integration.",
            "Static factory registries instantiate implementations with a parameterless constructor.",
            DiagnosticSeverity.Error,
            GeneratorCategory));

    public static DiagnosticDescriptor RegisterFactoryUnregisteredImplementation { get; } = Create(
        DiagnosticIds.RegisterFactoryUnregisteredImplementation,
        "Factory implementation is not registered",
        "Type '{0}' implements factory contract '{1}' but has no [RegisterFactory] attribute. Add [RegisterFactory(\"key\", typeof(...))] with a unique key and the contract type to register it.",
        "Concrete factory types should be registered when a contract registry is in use.",
        DiagnosticSeverity.Info,
        AnalyzerCategory);

    public static DiagnosticDescriptor RegistryKeyNotRegistered { get; } = Create(
        DiagnosticIds.RegistryKeyNotRegistered,
        "Registry key is not registered",
        "Key '{0}' is not registered for contract '{1}'. Registered keys: {2}.",
        "Registry lookups should use keys declared by [RegisterStrategy] or [RegisterFactory] attributes.",
        DiagnosticSeverity.Info,
        AnalyzerCategory);

    // State transition table (DP026–DP031)

    public static DiagnosticDescriptor StateTransitionDuplicateEdge { get; } = Create(
        DiagnosticIds.StateTransitionDuplicateEdge,
        "Duplicate state transition edge",
        "Transition from state '{0}' with trigger '{1}' is already declared. Remove the duplicate [Transition] attribute.",
        "Each (state, trigger) pair must map to at most one target state.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor StateTransitionInvalidStateMember { get; } = Create(
        DiagnosticIds.StateTransitionInvalidStateMember,
        "Transition state is not a declared enum member",
        "State value '{0}' is not a member of enum '{1}'. Use a declared enum member for [Transition] from/to arguments.",
        "Transition endpoints must reference declared state enum members.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor StateTransitionInvalidTriggerMember { get; } = Create(
        DiagnosticIds.StateTransitionInvalidTriggerMember,
        "Transition trigger is not a declared enum member",
        "Trigger value '{0}' is not a member of enum '{1}'. Use a declared enum member for [Transition] trigger arguments.",
        "Transition triggers must reference declared trigger enum members.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor StateTransitionInvalidInitialState { get; } = Create(
        DiagnosticIds.StateTransitionInvalidInitialState,
        "Initial state is not a declared enum member",
        "Initial state '{0}' is not a member of enum '{1}'. Set [StateMachine] Initial to a declared state enum member.",
        "State machines require a valid initial state enum member.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor StateMachineHolderInvalid { get; } = Create(
        DiagnosticIds.StateMachineHolderInvalid,
        "StateMachine holder must be a static partial class",
        "Class '{0}' must be declared static and partial so [StateMachine] can emit transition table members.",
        "StateMachine augments static partial holder classes with generated transition helpers.",
        DiagnosticSeverity.Error,
        GeneratorCategory);

    public static DiagnosticDescriptor StateTransitionIsolatedState { get; } = Create(
        DiagnosticIds.StateTransitionIsolatedState,
        "State is never used as a transition source",
        "State '{0}' is declared on enum '{1}' but never appears as a [Transition] from state. This is informational for terminal or reserved states.",
        "States without outgoing transitions may be intentional terminal states.",
        DiagnosticSeverity.Info,
        GeneratorCategory);

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string messageFormat,
        string description,
        DiagnosticSeverity severity,
        string category) =>
        new(
            id,
            title,
            messageFormat,
            category,
            severity,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"{DiagnosticsPage}#{id.ToLowerInvariant()}");
}

/// <summary>Grouped diagnostics for keyed registration generators (strategy / factory).</summary>
public readonly struct KeyedRegistrationDiagnostics
{
    public KeyedRegistrationDiagnostics(
        DiagnosticDescriptor duplicateKey,
        DiagnosticDescriptor contractMismatch,
        DiagnosticDescriptor missingParameterlessConstructor)
    {
        DuplicateKey = duplicateKey;
        ContractMismatch = contractMismatch;
        MissingParameterlessConstructor = missingParameterlessConstructor;
    }

    public DiagnosticDescriptor DuplicateKey { get; }

    public DiagnosticDescriptor ContractMismatch { get; }

    public DiagnosticDescriptor MissingParameterlessConstructor { get; }
}

#pragma warning restore CS1591
