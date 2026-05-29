using System.Collections.Generic;

namespace DesignPatterns;

/// <summary>
/// Shared read-only abstraction for keyed registries.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type stored in the registry.</typeparam>
public interface IReadOnlyRegistry<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Gets all registered keys.
    /// </summary>
    IReadOnlyCollection<TKey> Keys { get; }

    /// <summary>
    /// Tries to get a value for the given key.
    /// </summary>
    /// <param name="key">The registry key.</param>
    /// <param name="value">The resolved value when the key is registered.</param>
    /// <returns><see langword="true"/> when the key is registered; otherwise <see langword="false"/>.</returns>
    bool TryGet(TKey key, out TValue value);
}
