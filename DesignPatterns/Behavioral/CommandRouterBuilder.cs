using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Behavioral;

/// <summary>
/// Builds an immutable <see cref="ICommandRouter"/> with a 1:1 command-type → handler map
/// and optional per-command pipeline behavior onions.
/// </summary>
/// <remarks>
/// The builder is not thread-safe. The router returned by <see cref="Build"/> is safe for
/// concurrent <see cref="ICommandRouter.SendAsync{TCommand}"/> / <c>TrySendAsync</c> calls.
/// Pipeline behaviors are frozen into the handler map at <see cref="Build"/> time.
/// Lower behavior <c>order</c> values run first (outermost inbound).
/// </remarks>
public sealed class CommandRouterBuilder
{
    private readonly Dictionary<Type, object> _handlers = new();
    private readonly Dictionary<Type, IBehaviorComposer> _composers = new();
    private int _behaviorSequence;

    /// <summary>
    /// Registers a void-style handler for <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A handler is already registered for <typeparamref name="TCommand"/>.</exception>
    public CommandRouterBuilder Register<TCommand>(ICommandHandler<TCommand> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        AddHandler(typeof(TCommand), handler);
        return this;
    }

    /// <summary>
    /// Registers a result-producing handler for <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type used as the routing key.</typeparam>
    /// <typeparam name="TResult">The result type produced by the handler.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A handler is already registered for <typeparamref name="TCommand"/>.</exception>
    public CommandRouterBuilder Register<TCommand, TResult>(ICommandHandler<TCommand, TResult> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        AddHandler(typeof(TCommand), handler);
        return this;
    }

    /// <summary>
    /// Registers a void-style pipeline behavior for <typeparamref name="TCommand"/>.
    /// Lower <paramref name="order"/> values run first (outermost inbound).
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type this behavior applies to.</typeparam>
    /// <param name="behavior">The behavior instance.</param>
    /// <param name="order">Inbound order; lower values are outermost. Default is <c>0</c>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="behavior"/> is <see langword="null"/>.</exception>
    public CommandRouterBuilder UseBehavior<TCommand>(
        ICommandPipelineBehavior<TCommand> behavior,
        int order = 0)
    {
        if (behavior is null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        GetOrAddComposer(typeof(TCommand), () => new VoidBehaviorComposer<TCommand>())
            .Add(behavior, order, _behaviorSequence++);
        return this;
    }

    /// <summary>
    /// Registers a result-producing pipeline behavior for <typeparamref name="TCommand"/>.
    /// Lower <paramref name="order"/> values run first (outermost inbound).
    /// </summary>
    /// <typeparam name="TCommand">The command CLR type this behavior applies to.</typeparam>
    /// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
    /// <param name="behavior">The behavior instance.</param>
    /// <param name="order">Inbound order; lower values are outermost. Default is <c>0</c>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="behavior"/> is <see langword="null"/>.</exception>
    public CommandRouterBuilder UseBehavior<TCommand, TResult>(
        ICommandPipelineBehavior<TCommand, TResult> behavior,
        int order = 0)
    {
        if (behavior is null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        GetOrAddComposer(typeof(TCommand), () => new ResultBehaviorComposer<TCommand, TResult>())
            .Add(behavior, order, _behaviorSequence++);
        return this;
    }

    /// <summary>
    /// Builds an immutable command router, freezing any registered pipeline onions around terminal handlers.
    /// </summary>
    /// <returns>A thread-safe router for concurrent dispatch.</returns>
    /// <exception cref="InvalidOperationException">
    /// A pipeline behavior was registered for a command type that has no matching handler,
    /// or the handler contract does not match the behavior contract.
    /// </exception>
    public ICommandRouter Build()
    {
        var composed = new Dictionary<Type, object>(_handlers);

        foreach (var pair in _composers)
        {
            if (!composed.TryGetValue(pair.Key, out var registered))
            {
                throw new InvalidOperationException(
                    $"Pipeline behavior(s) were registered for command type '{pair.Key}', " +
                    "but no matching command handler is registered.");
            }

            composed[pair.Key] = pair.Value.Compose(registered);
        }

        return new CommandRouter(composed);
    }

    private TComposer GetOrAddComposer<TComposer>(Type commandType, Func<TComposer> factory)
        where TComposer : class, IBehaviorComposer
    {
        if (_composers.TryGetValue(commandType, out var existing))
        {
            if (existing is not TComposer typed)
            {
                throw new InvalidOperationException(
                    $"Pipeline behaviors for command type '{commandType}' mix void and result contracts. " +
                    "Register behaviors that match the handler contract.");
            }

            return typed;
        }

        var created = factory();
        _composers.Add(commandType, created);
        return created;
    }

    private void AddHandler(Type commandType, object handler)
    {
        if (_handlers.ContainsKey(commandType))
        {
            throw new ArgumentException(
                $"A command handler is already registered for command type '{commandType}'.",
                nameof(handler));
        }

        _handlers.Add(commandType, handler);
    }

    private interface IBehaviorComposer
    {
        object Compose(object registered);
    }

    private sealed class VoidBehaviorComposer<TCommand> : IBehaviorComposer
    {
        private readonly List<BehaviorEntry> _behaviors = new();

        public void Add(ICommandPipelineBehavior<TCommand> behavior, int order, int sequence) =>
            _behaviors.Add(new BehaviorEntry(behavior, order, sequence));

        public object Compose(object registered)
        {
            if (registered is not ICommandHandler<TCommand> handler)
            {
                throw new InvalidOperationException(
                    $"Pipeline behavior(s) for command type '{typeof(TCommand)}' require " +
                    $"ICommandHandler<{typeof(TCommand).Name}>, but a different handler contract is registered.");
            }

            _behaviors.Sort(static (a, b) =>
            {
                var orderCompare = a.Order.CompareTo(b.Order);
                return orderCompare != 0 ? orderCompare : a.Sequence.CompareTo(b.Sequence);
            });

            CommandPipelineDelegate<TCommand> pipeline = handler.HandleAsync;

            for (var i = _behaviors.Count - 1; i >= 0; i--)
            {
                var behavior = _behaviors[i].Behavior;
                var next = pipeline;
                pipeline = (command, cancellationToken) =>
                    behavior.InvokeAsync(command, next, cancellationToken);
            }

            return new PipelineVoidHandler<TCommand>(pipeline);
        }

        private readonly struct BehaviorEntry
        {
            public BehaviorEntry(ICommandPipelineBehavior<TCommand> behavior, int order, int sequence)
            {
                Behavior = behavior;
                Order = order;
                Sequence = sequence;
            }

            public ICommandPipelineBehavior<TCommand> Behavior { get; }
            public int Order { get; }
            public int Sequence { get; }
        }
    }

    private sealed class ResultBehaviorComposer<TCommand, TResult> : IBehaviorComposer
    {
        private readonly List<BehaviorEntry> _behaviors = new();

        public void Add(ICommandPipelineBehavior<TCommand, TResult> behavior, int order, int sequence) =>
            _behaviors.Add(new BehaviorEntry(behavior, order, sequence));

        public object Compose(object registered)
        {
            if (registered is not ICommandHandler<TCommand, TResult> handler)
            {
                throw new InvalidOperationException(
                    $"Pipeline behavior(s) for command type '{typeof(TCommand)}' require " +
                    $"ICommandHandler<{typeof(TCommand).Name}, {typeof(TResult).Name}>, " +
                    "but a different handler contract is registered.");
            }

            _behaviors.Sort(static (a, b) =>
            {
                var orderCompare = a.Order.CompareTo(b.Order);
                return orderCompare != 0 ? orderCompare : a.Sequence.CompareTo(b.Sequence);
            });

            CommandPipelineDelegate<TCommand, TResult> pipeline = handler.HandleAsync;

            for (var i = _behaviors.Count - 1; i >= 0; i--)
            {
                var behavior = _behaviors[i].Behavior;
                var next = pipeline;
                pipeline = (command, cancellationToken) =>
                    behavior.InvokeAsync(command, next, cancellationToken);
            }

            return new PipelineResultHandler<TCommand, TResult>(pipeline);
        }

        private readonly struct BehaviorEntry
        {
            public BehaviorEntry(ICommandPipelineBehavior<TCommand, TResult> behavior, int order, int sequence)
            {
                Behavior = behavior;
                Order = order;
                Sequence = sequence;
            }

            public ICommandPipelineBehavior<TCommand, TResult> Behavior { get; }
            public int Order { get; }
            public int Sequence { get; }
        }
    }

    private sealed class PipelineVoidHandler<TCommand> : ICommandHandler<TCommand>
    {
        private readonly CommandPipelineDelegate<TCommand> _pipeline;

        public PipelineVoidHandler(CommandPipelineDelegate<TCommand> pipeline) =>
            _pipeline = pipeline;

        public ValueTask HandleAsync(TCommand command, CancellationToken cancellationToken = default) =>
            _pipeline(command, cancellationToken);
    }

    private sealed class PipelineResultHandler<TCommand, TResult> : ICommandHandler<TCommand, TResult>
    {
        private readonly CommandPipelineDelegate<TCommand, TResult> _pipeline;

        public PipelineResultHandler(CommandPipelineDelegate<TCommand, TResult> pipeline) =>
            _pipeline = pipeline;

        public ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default) =>
            _pipeline(command, cancellationToken);
    }
}
