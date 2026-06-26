using System;
using System.Diagnostics.CodeAnalysis;

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

    /// <summary>
    /// Tries to resolve a strategy for the given key, applying any registered
    /// static guard predicate. Returns <see langword="false"/> when the key is
    /// not registered or when the guard predicate returns <see langword="false"/>.
    /// </summary>
    /// <param name="key">The strategy key.</param>
    /// <param name="strategy">The resolved strategy, or <c>default</c> when not found or guard failed.</param>
    /// <returns><see langword="true"/> when the strategy is available and its guard passes.</returns>
    bool TryGetWithGuard(TKey key, [MaybeNullWhen(false)] out TStrategy strategy);
}
