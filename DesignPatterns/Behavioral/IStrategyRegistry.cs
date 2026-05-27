using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Read-only registry that resolves strategy implementations by key.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy implementation type.</typeparam>
public interface IStrategyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    /// <summary>
    /// Gets all registered keys.
    /// </summary>
    IReadOnlyCollection<TKey> Keys { get; }

    /// <summary>
    /// Tries to resolve a strategy for the given key.
    /// </summary>
    bool TryGet(TKey key, out TStrategy strategy);

    /// <summary>
    /// Resolves a strategy for the given key.
    /// </summary>
    /// <exception cref="StrategyNotFoundException">When the key is not registered.</exception>
    TStrategy Get(TKey key);
}
