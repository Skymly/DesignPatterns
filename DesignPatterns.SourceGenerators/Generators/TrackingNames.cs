namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Names that are attached to incremental generator stages via
/// <c>WithTrackingName</c> for cache-hit observability and testing.
/// </summary>
internal static class TrackingNames
{
    // GenerateSingletonGenerator
    public const string SingletonTransform = nameof(SingletonTransform);

    // RegisterFactoryGenerator
    public const string FactoryNonGenericTransform = nameof(FactoryNonGenericTransform);
    public const string FactoryGenericTransform = nameof(FactoryGenericTransform);

    // RegisterStrategyGenerator
    public const string StrategyNonGenericTransform = nameof(StrategyNonGenericTransform);
    public const string StrategyGenericTransform = nameof(StrategyGenericTransform);

    // HandlerOrderGenerator
    public const string HandlerNonGenericTransform = nameof(HandlerNonGenericTransform);
    public const string HandlerGenericTransform = nameof(HandlerGenericTransform);

    // CompositePartGenerator
    public const string CompositeNonGenericTransform = nameof(CompositeNonGenericTransform);
    public const string CompositeGenericTransform = nameof(CompositeGenericTransform);

    // DecoratorGenerator
    public const string DecoratorNonGenericTransform = nameof(DecoratorNonGenericTransform);
    public const string DecoratorGenericTransform = nameof(DecoratorGenericTransform);

    // StateTransitionGenerator
    public const string StateMachineTransform = nameof(StateMachineTransform);

    // RegisterEventHandlerGenerator
    public const string EventHandlerNonGenericTransform = nameof(EventHandlerNonGenericTransform);
    public const string EventHandlerGenericTransform = nameof(EventHandlerGenericTransform);

    // Collect + Combine stages (shared naming: {Generator}Collect, {Generator}Combine)
    public const string SingletonCollect = nameof(SingletonCollect);

    public const string FactoryCollect = nameof(FactoryCollect);
    public const string FactoryCombine = nameof(FactoryCombine);

    public const string StrategyCollect = nameof(StrategyCollect);
    public const string StrategyCombine = nameof(StrategyCombine);

    public const string HandlerCollect = nameof(HandlerCollect);
    public const string HandlerCombine = nameof(HandlerCombine);

    public const string CompositeCollect = nameof(CompositeCollect);
    public const string CompositeCombine = nameof(CompositeCombine);

    public const string DecoratorCollect = nameof(DecoratorCollect);
    public const string DecoratorCombine = nameof(DecoratorCombine);

    public const string StateMachineCollect = nameof(StateMachineCollect);
    public const string StateMachineCombine = nameof(StateMachineCombine);

    public const string EventHandlerCollect = nameof(EventHandlerCollect);
    public const string EventHandlerCombine = nameof(EventHandlerCombine);
}
