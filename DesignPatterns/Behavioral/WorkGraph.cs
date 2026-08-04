using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

internal sealed class WorkStepRegistration<TContext>
{
    public WorkStepRegistration(string id, IWorkStep<TContext> step, string[] dependsOn)
    {
        Id = id;
        Step = step;
        DependsOn = dependsOn;
    }

    public string Id { get; }

    public IWorkStep<TContext> Step { get; }

    public string[] DependsOn { get; }
}

internal sealed class WorkGraph<TContext> : IWorkGraph<TContext>
{
    private readonly IReadOnlyList<IReadOnlyList<WorkStepRegistration<TContext>>> _waves;

    public WorkGraph(IReadOnlyList<IReadOnlyList<WorkStepRegistration<TContext>>> waves)
    {
        _waves = waves ?? throw new ArgumentNullException(nameof(waves));
    }

    public async ValueTask RunAsync(TContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var wave in _waves)
        {
            if (wave.Count == 1)
            {
                await wave[0].Step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                continue;
            }

            using var waveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = waveCts.Token;
            var tasks = new Task[wave.Count];

            for (var i = 0; i < wave.Count; i++)
            {
                var step = wave[i].Step;
                tasks[i] = ExecuteWithFailFastAsync(step, context, waveCts, token);
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();

                Exception? failure = null;
                foreach (var task in tasks)
                {
                    if (!task.IsFaulted || task.Exception is null)
                    {
                        continue;
                    }

                    var inner = Unwrap(task.Exception);
                    if (inner is OperationCanceledException && token.IsCancellationRequested)
                    {
                        continue;
                    }

                    failure ??= inner;
                }

                if (failure is not null)
                {
                    throw failure;
                }

                throw;
            }
        }
    }

    private static async Task ExecuteWithFailFastAsync(
        IWorkStep<TContext> step,
        TContext context,
        CancellationTokenSource waveCts,
        CancellationToken token)
    {
        try
        {
            await step.ExecuteAsync(context, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Cancel peers immediately so fail-fast does not wait for WhenAll.
            if (ex is not OperationCanceledException || !token.IsCancellationRequested)
            {
                try
                {
                    waveCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // ignored
                }
            }

            throw;
        }
    }

    private static Exception Unwrap(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Count == 1
                ? aggregate.InnerExceptions[0]
                : aggregate.Flatten();
        }

        return exception;
    }
}
