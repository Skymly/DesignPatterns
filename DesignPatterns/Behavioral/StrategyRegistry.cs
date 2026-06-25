using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Linq;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Immutable <see cref="IStrategyRegistry{TKey,TStrategy}"/> backed by a read-only dictionary.
/// On net8.0+ the dictionary is frozen for faster lookups.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy implementation type.</typeparam>
public sealed class StrategyRegistry<TKey, TStrategy> : IStrategyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    private readonly IReadOnlyDictionary<TKey, TStrategy> _strategies;
    private readonly IReadOnlyDictionary<TKey, Func<TKey, bool>>? _guards;

    /// <summary>
    /// Initializes a new instance from an existing dictionary.
    /// </summary>
    public StrategyRegistry(IReadOnlyDictionary<TKey, TStrategy> strategies)
        : this(strategies, guards: null)
    {
    }

    /// <summary>
    /// Initializes a new instance from an existing dictionary with optional
    /// static guard predicates keyed by strategy key.
    /// </summary>
    public StrategyRegistry(
        IReadOnlyDictionary<TKey, TStrategy> strategies,
        IReadOnlyDictionary<TKey, Func<TKey, bool>>? guards)
    {
        var dict = strategies ?? throw new ArgumentNullException(nameof(strategies));
#if NET8_0_OR_GREATER
        _strategies = dict is FrozenDictionary<TKey, TStrategy> frozen
            ? frozen
            : dict.ToFrozenDictionary();
#else
        _strategies = dict;
#endif
        _guards = guards;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys => _strategies.Keys.ToArray();

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
    public bool TryGetWithGuard(TKey key, out TStrategy strategy)
    {
        if (!_strategies.TryGetValue(key, out var value))
        {
            strategy = default!;
            return false;
        }

        if (_guards is not null
            && _guards.TryGetValue(key, out var guard)
            && !guard(key))
        {
            strategy = default!;
            return false;
        }

        strategy = value;
        return true;
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
