namespace DesignPatterns.Structural;

/// <summary>
/// Wraps an inner service instance with the same <typeparamref name="TService"/> contract.
/// </summary>
/// <typeparam name="TService">The service contract type.</typeparam>
public interface IDecorator<TService>
    where TService : class
{
    /// <summary>
    /// Returns a new <typeparamref name="TService"/> that delegates to <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The inner service instance.</param>
    TService Decorate(TService inner);
}
