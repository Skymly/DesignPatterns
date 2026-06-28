using System;
using System.Diagnostics;
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

    // --- Traced execution (async) ---

    /// <summary>
    /// Resolves a strategy by key and executes it asynchronously, returning a
    /// trace of the resolution and execution outcome. When the strategy throws,
    /// the exception is captured in the trace and re-thrown.
    /// </summary>
    public static async ValueTask<StrategyExecutionTrace<TOutput>> ExecuteTracedAsync<TKey, TInput, TOutput>(
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

        return await ExecuteTracedAsyncCore(registry, key, input, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a strategy by key and executes it asynchronously, returning a
    /// trace and notifying <paramref name="observer"/> of the outcome.
    /// </summary>
    public static async ValueTask<StrategyExecutionTrace<TOutput>> ExecuteTracedAsync<TKey, TInput, TOutput>(
        this IStrategyRegistry<TKey, IAsyncStrategy<TInput, TOutput>> registry,
        TKey key,
        TInput input,
        IStrategyExecutionObserver<TInput, TOutput>? observer,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        return await ExecuteTracedAsyncCore(registry, key, input, observer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a derived async strategy contract by key and executes it with tracing.
    /// </summary>
    public static async ValueTask<StrategyExecutionTrace<TOutput>> ExecuteTracedAsync<TContract, TOutput, TInput>(
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

        return await ExecuteTracedAsyncCore(
            registry, key, input, null, cancellationToken,
            (strategy, inp, ct) => strategy.ExecuteAsync(inp, ct)).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a derived async strategy contract by key and executes it with tracing,
    /// notifying <paramref name="observer"/> of the outcome.
    /// </summary>
    public static async ValueTask<StrategyExecutionTrace<TOutput>> ExecuteTracedAsync<TContract, TOutput, TInput>(
        this IStrategyRegistry<string, TContract> registry,
        string key,
        TInput input,
        IStrategyExecutionObserver<TInput, TOutput>? observer,
        CancellationToken cancellationToken = default)
        where TContract : IAsyncStrategy<TInput, TOutput>
    {
        if (registry is null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        return await ExecuteTracedAsyncCore(
            registry, key, input, observer, cancellationToken,
            (strategy, inp, ct) => strategy.ExecuteAsync(inp, ct)).ConfigureAwait(false);
    }

    private static async ValueTask<StrategyExecutionTrace<TOutput>> ExecuteTracedAsyncCore<TContract, TOutput, TInput>(
        IStrategyRegistry<string, TContract> registry,
        string key,
        TInput input,
        IStrategyExecutionObserver<TInput, TOutput>? observer,
        CancellationToken cancellationToken,
        Func<TContract, TInput, CancellationToken, ValueTask<TOutput>> executor)
        where TContract : IAsyncStrategy<TInput, TOutput>
    {
        var sw = Stopwatch.StartNew();

        if (!registry.TryGetWithGuard(key, out var strategy))
        {
            sw.Stop();
            var notFound = !registry.TryGet(key, out _);
            var status = notFound
                ? StrategyExecutionStepStatus.KeyNotFound
                : StrategyExecutionStepStatus.GuardRejected;
            var trace = new StrategyExecutionTrace<TOutput>(key, status, default, null, sw.ElapsedMilliseconds);
            observer?.OnExecutionFailed(key, input, trace);
            return trace;
        }

        try
        {
            var output = await executor(strategy, input, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            var trace = new StrategyExecutionTrace<TOutput>(key, StrategyExecutionStepStatus.Executed, output, null, sw.ElapsedMilliseconds);
            observer?.OnExecutionCompleted(key, input, output, sw.ElapsedMilliseconds);
            return trace;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var trace = new StrategyExecutionTrace<TOutput>(key, StrategyExecutionStepStatus.Failed, default, ex, sw.ElapsedMilliseconds);
            observer?.OnExecutionFailed(key, input, trace);
            throw;
        }
    }

    private static async ValueTask<StrategyExecutionTrace<TOutput>> ExecuteTracedAsyncCore<TKey, TInput, TOutput>(
        IStrategyRegistry<TKey, IAsyncStrategy<TInput, TOutput>> registry,
        TKey key,
        TInput input,
        IStrategyExecutionObserver<TInput, TOutput>? observer,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var sw = Stopwatch.StartNew();

        if (!registry.TryGetWithGuard(key, out var strategy))
        {
            sw.Stop();
            var notFound = !registry.TryGet(key, out _);
            var status = notFound
                ? StrategyExecutionStepStatus.KeyNotFound
                : StrategyExecutionStepStatus.GuardRejected;
            var keyStr = key?.ToString() ?? string.Empty;
            var trace = new StrategyExecutionTrace<TOutput>(keyStr, status, default, null, sw.ElapsedMilliseconds);
            observer?.OnExecutionFailed(keyStr, input, trace);
            return trace;
        }

        try
        {
            var output = await strategy.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            var keyStr = key?.ToString() ?? string.Empty;
            var trace = new StrategyExecutionTrace<TOutput>(keyStr, StrategyExecutionStepStatus.Executed, output, null, sw.ElapsedMilliseconds);
            observer?.OnExecutionCompleted(keyStr, input, output, sw.ElapsedMilliseconds);
            return trace;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var keyStr = key?.ToString() ?? string.Empty;
            var trace = new StrategyExecutionTrace<TOutput>(keyStr, StrategyExecutionStepStatus.Failed, default, ex, sw.ElapsedMilliseconds);
            observer?.OnExecutionFailed(keyStr, input, trace);
            throw;
        }
    }
}
