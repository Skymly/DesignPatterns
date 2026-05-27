using System;
using System.Collections.Generic;
namespace DesignPatterns.Behavioral;

/// <summary>
/// Immutable <see cref="IStrategyRegistry{TKey,TStrategy}"/> backed by a read-only dictionary.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy implementation type.</typeparam>
public sealed class StrategyRegistry<TKey, TStrategy> : IStrategyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, TStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance from an existing dictionary.
    /// </summary>
    public StrategyRegistry(IReadOnlyDictionary<TKey, TStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys => (IReadOnlyCollection<TKey>)_strategies.Keys;

    /// <inheritdoc />
    public bool TryGet(TKey key, out TStrategy strategy)
    {
        if (_strategies.TryGetValue(key, out var value))
        {
            strategy = value;
            return true;
        }

        strategy = default!;
        return false;
    }

    /// <inheritdoc />
    public TStrategy Get(TKey key)
    {
        if (TryGet(key, out var strategy))
        {
            return strategy;
        }

        throw StrategyNotFoundException.ForKey(key);
    }
}
