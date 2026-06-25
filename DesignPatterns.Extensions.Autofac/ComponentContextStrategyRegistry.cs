using System;
using System.Collections.Generic;
using Autofac;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac;

/// <summary>
/// Resolves strategy implementations from <see cref="ILifetimeScope"/> on each lookup.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy contract type.</typeparam>
public sealed class ComponentContextStrategyRegistry<TKey, TStrategy> : IStrategyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    private readonly ILifetimeScope _lifetimeScope;
    private readonly IReadOnlyList<(TKey Key, Type ImplementationType)> _entries;
    private readonly Dictionary<TKey, Type> _typeByKey;
    private readonly IReadOnlyDictionary<TKey, Func<TKey, bool>>? _guards;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ComponentContextStrategyRegistry(
        ILifetimeScope lifetimeScope,
        IReadOnlyList<(TKey Key, Type ImplementationType)> entries)
        : this(lifetimeScope, entries, guards: null)
    {
    }

    /// <summary>
    /// Initializes a new instance with optional static guard predicates.
    /// </summary>
    public ComponentContextStrategyRegistry(
        ILifetimeScope lifetimeScope,
        IReadOnlyList<(TKey Key, Type ImplementationType)> entries,
        IReadOnlyDictionary<TKey, Func<TKey, bool>>? guards)
    {
        _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _typeByKey = new Dictionary<TKey, Type>(entries.Count);
        foreach (var (key, implementationType) in entries)
        {
            _typeByKey[key] = implementationType;
        }

        _guards = guards;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TKey> Keys
    {
        get
        {
            var keys = new TKey[_entries.Count];
            for (var i = 0; i < _entries.Count; i++)
            {
                keys[i] = _entries[i].Key;
            }

            return keys;
        }
    }

    /// <inheritdoc />
    public bool TryGet(TKey key, out TStrategy strategy)
    {
        if (!_typeByKey.TryGetValue(key, out var implementationType))
        {
            strategy = default!;
            return false;
        }

        strategy = (TStrategy)_lifetimeScope.Resolve(implementationType);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetWithGuard(TKey key, out TStrategy strategy)
    {
        if (_guards is not null
            && _guards.TryGetValue(key, out var guard)
            && !guard(key))
        {
            strategy = default!;
            return false;
        }

        return TryGet(key, out strategy);
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
