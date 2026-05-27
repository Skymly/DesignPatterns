using System;
using System.Collections.Generic;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Builds an immutable <see cref="IStrategyRegistry{TKey,TStrategy}"/>.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TStrategy">Strategy implementation type.</typeparam>
public sealed class StrategyRegistryBuilder<TKey, TStrategy>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TStrategy> _strategies = new();

    /// <summary>
    /// Registers a strategy instance for the given key.
    /// </summary>
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, TStrategy strategy)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (strategy is null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        if (_strategies.ContainsKey(key))
        {
            throw new ArgumentException($"A strategy is already registered for key '{key}'.", nameof(key));
        }

        _strategies.Add(key, strategy);
        return this;
    }

    /// <summary>
    /// Registers a strategy factory for the given key. The factory is invoked once at build time.
    /// </summary>
    public StrategyRegistryBuilder<TKey, TStrategy> Register(TKey key, Func<TStrategy> factory)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        return Register(key, factory());
    }

    /// <summary>
    /// Builds the registry.
    /// </summary>
    public IStrategyRegistry<TKey, TStrategy> Build() =>
        new StrategyRegistry<TKey, TStrategy>(_strategies);
}
