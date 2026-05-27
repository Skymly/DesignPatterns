namespace DesignPatterns.Structural;

/// <summary>
/// Optional marker for types that decorate <typeparamref name="TService"/>.
/// Source generators do not require this interface.
/// </summary>
/// <typeparam name="TService">The service contract type.</typeparam>
public interface IDecoratorOf<TService> : IDecorator<TService>
    where TService : class;
