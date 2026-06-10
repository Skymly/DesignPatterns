using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Convenience extensions for resolving and executing <see cref="IAsyncStrategy{TInput,TOutput}"/> from a registry.
/// </summary>
public static class StrategyRegistryExtensions
{
    /// <summary>
    /// Resolves a strategy by key and executes it asynchronously.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TInput">Strategy input type.</typeparam>
    /// <typeparam name="TOutput">Strategy output type.</typeparam>
    /// <param name="registry">Strategy registry.</param>
    /// <param name="key">Strategy key.</param>
    /// <param name="input">Input passed to the strategy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The strategy result.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="registry"/> is null.</exception>
    /// <exception cref="StrategyNotFoundException">When the key is not registered.</exception>
    public static ValueTask<TOutput> ExecuteAsync<TKey, TInput, TOutput>(
        this IStrategyRegistry<TKey, IAsyncStrategy<TInput, TOutput>> registry,
        TKey key,
        TInput input,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        return registry.Get(key).ExecuteAsync(input, cancellationToken);
    }

    /// <summary>
    /// Resolves a derived async strategy contract by key and executes it asynchronously.
    /// Specify <typeparamref name="TContract"/> and <typeparamref name="TOutput"/> when the contract extends <see cref="IAsyncStrategy{TInput,TOutput}"/>.
    /// </summary>
    public static ValueTask<TOutput> ExecuteAsync<TContract, TOutput, TInput>(
        this IStrategyRegistry<string, TContract> registry,
        string key,
        TInput input,
        CancellationToken cancellationToken = default)
        where TContract : IAsyncStrategy<TInput, TOutput>
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        return registry.Get(key).ExecuteAsync(input, cancellationToken);
    }

    /// <summary>
    /// Resolves a derived async strategy contract by key and executes it asynchronously.
    /// Specify all type parameters when the contract extends <see cref="IAsyncStrategy{TInput,TOutput}"/>.
    /// </summary>
    public static ValueTask<TOutput> ExecuteAsync<TContract, TOutput, TKey, TInput>(
        this IStrategyRegistry<TKey, TContract> registry,
        TKey key,
        TInput input,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TContract : IAsyncStrategy<TInput, TOutput>
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        return registry.Get(key).ExecuteAsync(input, cancellationToken);
    }

    /// <summary>
    /// Attempts to resolve a strategy by key and execute it asynchronously.
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TInput">Strategy input type.</typeparam>
    /// <typeparam name="TOutput">Strategy output type.</typeparam>
    /// <param name="registry">Strategy registry.</param>
    /// <param name="key">Strategy key.</param>
    /// <param name="input">Input passed to the strategy.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the strategy result; otherwise default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the key is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="registry"/> is null.</exception>
    public static bool TryExecuteAsync<TKey, TInput, TOutput>(
        this IStrategyRegistry<TKey, IAsyncStrategy<TInput, TOutput>> registry,
        TKey key,
        TInput input,
        out ValueTask<TOutput> result,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (registry.TryGet(key, out var strategy))
        {
            result = strategy.ExecuteAsync(input, cancellationToken);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to resolve a derived async strategy contract by key and execute it asynchronously.
    /// </summary>
    public static bool TryExecuteAsync<TContract, TOutput, TInput>(
        this IStrategyRegistry<string, TContract> registry,
        string key,
        TInput input,
        out ValueTask<TOutput> result,
        CancellationToken cancellationToken = default)
        where TContract : IAsyncStrategy<TInput, TOutput>
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (registry.TryGet(key, out var strategy))
        {
            result = strategy.ExecuteAsync(input, cancellationToken);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to resolve a derived async strategy contract by key and execute it asynchronously.
    /// </summary>
    public static bool TryExecuteAsync<TContract, TOutput, TKey, TInput>(
        this IStrategyRegistry<TKey, TContract> registry,
        TKey key,
        TInput input,
        out ValueTask<TOutput> result,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TContract : IAsyncStrategy<TInput, TOutput>
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        if (registry.TryGet(key, out var strategy))
        {
            result = strategy.ExecuteAsync(input, cancellationToken);
            return true;
        }

        result = default;
        return false;
    }
}
