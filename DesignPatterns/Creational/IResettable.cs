namespace DesignPatterns.Creational;

/// <summary>
/// Contract for pooled objects that need to be reset before reuse.
/// When a product implements this interface, <see cref="Reset"/> is called
/// by <see cref="IPooledFactoryRegistry{TKey,TProduct}.Return"/> before the
/// product is returned to the pool.
/// </summary>
public interface IResettable
{
    /// <summary>
    /// Resets the object to its initial state for pool reuse.
    /// </summary>
    void Reset();
}
