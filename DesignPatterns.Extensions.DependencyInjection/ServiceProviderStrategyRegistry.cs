using System;
using System.Collections.Generic;
using DesignPatterns.Behavioral;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Extensions.DependencyInjection;

/// <summary>
/// Resolves strategy implementations from <see cref="IServiceProvider"/> on each lookup.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy contract type.</typeparam>
public sealed class ServiceProviderStrategyRegistry<TKey, TStrategy> : IStrategyRegistry<TKey, TStrategy>
    where TKey : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<(TKey Key, Type ImplementationType)> _entries;
    private readonly Dictionary<TKey, Type> _typeByKey;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ServiceProviderStrategyRegistry(
        IServiceProvider serviceProvider,
        IReadOnlyList<(TKey Key, Type ImplementationType)> entries)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _typeByKey = new Dictionary<TKey, Type>(entries.Count);
        foreach (var (key, implementationType) in entries)
        {
            _typeByKey[key] = implementationType;
        }
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

        strategy = (TStrategy)_serviceProvider.GetRequiredService(implementationType);
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
