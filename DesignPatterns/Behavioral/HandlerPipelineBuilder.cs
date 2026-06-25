using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Builds an immutable <see cref="HandlerPipeline{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
public sealed class HandlerPipelineBuilder<TContext>
{
    private readonly List<HandlerPipelineRegistration<TContext>> _handlers = new();

    /// <summary>
    /// Adds a handler to the pipeline. Handlers run in registration order.
    /// </summary>
    public HandlerPipelineBuilder<TContext> Use(IHandler<TContext> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _handlers.Add(new HandlerPipelineRegistration<TContext>(handler, handler.GetType().Name));
        return this;
    }

    /// <summary>
    /// Adds a handler to the pipeline with an optional guard predicate.
    /// When the guard returns <see langword="false"/>, the handler is skipped
    /// and the pipeline continues to the next handler.
    /// </summary>
    public HandlerPipelineBuilder<TContext> Use(IHandler<TContext> handler, Func<TContext, bool>? guard)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _handlers.Add(new HandlerPipelineRegistration<TContext>(handler, handler.GetType().Name, guard));
        return this;
    }

    /// <summary>
    /// Adds a handler delegate to the pipeline. Handlers run in registration order.
    /// </summary>
    public HandlerPipelineBuilder<TContext> Use(
        Func<TContext, HandlerDelegate<TContext>, CancellationToken, ValueTask> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return Use(new DelegateHandler(handler), "<delegate>");
    }

    /// <summary>
    /// Builds the pipeline.
    /// </summary>
    public HandlerPipeline<TContext> Build() => new(_handlers);

    private HandlerPipelineBuilder<TContext> Use(IHandler<TContext> handler, string displayName)
    {
        _handlers.Add(new HandlerPipelineRegistration<TContext>(handler, displayName));
        return this;
    }

    private sealed class DelegateHandler : IHandler<TContext>
    {
        public DelegateHandler(Func<TContext, HandlerDelegate<TContext>, CancellationToken, ValueTask> handler) =>
            Handler = handler;

        public Func<TContext, HandlerDelegate<TContext>, CancellationToken, ValueTask> Handler { get; }

        public ValueTask InvokeAsync(
            TContext context,
            HandlerDelegate<TContext> next,
            CancellationToken cancellationToken = default) =>
            Handler(context, next, cancellationToken);
    }
}
