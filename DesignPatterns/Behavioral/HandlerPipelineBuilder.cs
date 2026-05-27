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
    private readonly List<IHandler<TContext>> _handlers = new();

    /// <summary>
    /// Adds a handler to the pipeline. Handlers run in registration order.
    /// </summary>
    public HandlerPipelineBuilder<TContext> Use(IHandler<TContext> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _handlers.Add(handler);
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

        return Use(new DelegateHandler(handler));
    }

    /// <summary>
    /// Builds the pipeline.
    /// </summary>
    public HandlerPipeline<TContext> Build() => new(_handlers);

    private sealed class DelegateHandler : IHandler<TContext>
    {
        private readonly Func<TContext, HandlerDelegate<TContext>, CancellationToken, ValueTask> _handler;

        public DelegateHandler(Func<TContext, HandlerDelegate<TContext>, CancellationToken, ValueTask> handler) =>
            _handler = handler;

        public ValueTask InvokeAsync(
            TContext context,
            HandlerDelegate<TContext> next,
            CancellationToken cancellationToken = default) =>
            _handler(context, next, cancellationToken);
    }
}
