using DesignPatterns.Creational;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Read-only registry that resolves strategy implementations by key.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy implementation type.</typeparam>
public interface IStrategyRegistry<TKey, TStrategy> : IReadOnlyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    /// <summary>
    /// Resolves a strategy for the given key.
    /// </summary>
    /// <exception cref="StrategyNotFoundException">When the key is not registered.</exception>
    TStrategy Get(TKey key);
}
