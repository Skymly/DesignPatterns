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
}
