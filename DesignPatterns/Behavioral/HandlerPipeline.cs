using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// An immutable handler pipeline. Handlers run in registration order (first registered runs first).
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
public sealed class HandlerPipeline<TContext>
{
    private readonly HandlerDelegate<TContext> _pipeline;

    internal HandlerPipeline(IReadOnlyList<IHandler<TContext>> handlers)
    {
        if (handlers is null)
        {
            throw new ArgumentNullException(nameof(handlers));
        }

        _pipeline = BuildPipeline(handlers);
    }

    /// <summary>
    /// Invokes the pipeline with the given context.
    /// </summary>
    public ValueTask InvokeAsync(TContext context, CancellationToken cancellationToken = default) =>
        _pipeline(context, cancellationToken);

    private static HandlerDelegate<TContext> BuildPipeline(IReadOnlyList<IHandler<TContext>> handlers)
    {
        HandlerDelegate<TContext> pipeline = static (_, _) => default;

        for (var i = handlers.Count - 1; i >= 0; i--)
        {
            var handler = handlers[i];
            var next = pipeline;
            pipeline = (context, cancellationToken) => handler.InvokeAsync(context, next, cancellationToken);
        }

        return pipeline;
    }
}
