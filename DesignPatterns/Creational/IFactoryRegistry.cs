namespace DesignPatterns.Creational;

/// <summary>
/// Read-only registry that creates product instances by key using registered factories.
/// Each <see cref="Create"/> or successful <see cref="TryCreate"/> invokes the factory delegate (new instance per call).
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TProduct">Product type created by factories.</typeparam>
public interface IFactoryRegistry<TKey, TProduct> : IReadOnlyRegistry<TKey, TProduct>
    where TKey : notnull
{
    /// <summary>
    /// Tries to create a product for the given key.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <param name="product">The created product when the key is registered.</param>
    /// <returns><see langword="true"/> when the key is registered; otherwise <see langword="false"/>.</returns>
    bool TryCreate(TKey key, out TProduct product);

    /// <summary>
    /// Creates a product for the given key.
    /// </summary>
    /// <param name="key">The factory key.</param>
    /// <returns>A new product instance.</returns>
    /// <exception cref="FactoryNotFoundException">When the key is not registered.</exception>
    TProduct Create(TKey key);
}
