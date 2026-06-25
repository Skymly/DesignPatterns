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
    private readonly IReadOnlyList<HandlerPipelineRegistration<TContext>> _registrations;
    private readonly HandlerDelegate<TContext> _pipeline;

    internal HandlerPipeline(IReadOnlyList<HandlerPipelineRegistration<TContext>> registrations)
    {
        if (registrations is null)
        {
            throw new ArgumentNullException(nameof(registrations));
        }

        _registrations = registrations;
        _pipeline = BuildPipeline(registrations);
    }

    /// <summary>
    /// Invokes the pipeline with the given context.
    /// </summary>
    public ValueTask InvokeAsync(TContext context, CancellationToken cancellationToken = default) =>
        _pipeline(context, cancellationToken);

    /// <summary>
    /// Invokes the pipeline and returns a trace of handler outcomes (continued, short-circuited, or not reached).
    /// </summary>
    public async ValueTask<HandlerPipelineTrace> InvokeTracedAsync(
        TContext context,
        CancellationToken cancellationToken = default)
    {
        var traceBuilder = new HandlerPipelineTraceBuilder(_registrations);
        var pipeline = BuildTracedPipeline(_registrations, traceBuilder);
        await pipeline(context, cancellationToken).ConfigureAwait(false);
        return traceBuilder.ToTrace();
    }

    private static HandlerDelegate<TContext> BuildPipeline(
        IReadOnlyList<HandlerPipelineRegistration<TContext>> registrations)
    {
        HandlerDelegate<TContext> pipeline = static (_, _) => default;

        for (var i = registrations.Count - 1; i >= 0; i--)
        {
            var registration = registrations[i];
            var next = pipeline;
            var guard = registration.Guard;

            if (guard is null)
            {
                var handler = registration.Handler;
                pipeline = (context, cancellationToken) => handler.InvokeAsync(context, next, cancellationToken);
            }
            else
            {
                var handler = registration.Handler;
                pipeline = (context, cancellationToken) =>
                    guard(context)
                        ? handler.InvokeAsync(context, next, cancellationToken)
                        : next(context, cancellationToken);
            }
        }

        return pipeline;
    }

    private static HandlerDelegate<TContext> BuildTracedPipeline(
        IReadOnlyList<HandlerPipelineRegistration<TContext>> registrations,
        HandlerPipelineTraceBuilder traceBuilder)
    {
        HandlerDelegate<TContext> pipeline = static (_, _) => default;

        for (var i = registrations.Count - 1; i >= 0; i--)
        {
            var index = i;
            var registration = registrations[i];
            var next = pipeline;
            var guard = registration.Guard;

            if (guard is null)
            {
                pipeline = async (context, cancellationToken) =>
                {
                    var nextInvoked = false;
                    HandlerDelegate<TContext> tracedNext = (ctx, ct) =>
                    {
                        nextInvoked = true;
                        return next(ctx, ct);
                    };

                    await registration.Handler
                        .InvokeAsync(context, tracedNext, cancellationToken)
                        .ConfigureAwait(false);

                    traceBuilder.Record(
                        index,
                        nextInvoked
                            ? HandlerPipelineStepStatus.Completed
                            : HandlerPipelineStepStatus.ShortCircuited);
                };
            }
            else
            {
                pipeline = async (context, cancellationToken) =>
                {
                    if (!guard(context))
                    {
                        traceBuilder.Record(index, HandlerPipelineStepStatus.Skipped);
                        await next(context, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    var nextInvoked = false;
                    HandlerDelegate<TContext> tracedNext = (ctx, ct) =>
                    {
                        nextInvoked = true;
                        return next(ctx, ct);
                    };

                    await registration.Handler
                        .InvokeAsync(context, tracedNext, cancellationToken)
                        .ConfigureAwait(false);

                    traceBuilder.Record(
                        index,
                        nextInvoked
                            ? HandlerPipelineStepStatus.Completed
                            : HandlerPipelineStepStatus.ShortCircuited);
                };
            }
        }

        return pipeline;
    }

    private sealed class HandlerPipelineTraceBuilder
    {
        private readonly string[] _names;
        private readonly HandlerPipelineStepStatus[] _statuses;

        public HandlerPipelineTraceBuilder(IReadOnlyList<HandlerPipelineRegistration<TContext>> registrations)
        {
            _names = new string[registrations.Count];
            _statuses = new HandlerPipelineStepStatus[registrations.Count];

            for (var i = 0; i < registrations.Count; i++)
            {
                _names[i] = registrations[i].DisplayName;
                _statuses[i] = HandlerPipelineStepStatus.NotReached;
            }
        }

        public void Record(int index, HandlerPipelineStepStatus status) =>
            _statuses[index] = status;

        public HandlerPipelineTrace ToTrace()
        {
            var steps = new HandlerPipelineStep[_names.Length];

            for (var i = 0; i < _names.Length; i++)
            {
                steps[i] = new HandlerPipelineStep(i, _names[i], _statuses[i]);
            }

            return new HandlerPipelineTrace(steps);
        }
    }
}
