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
    /// Invokes the pipeline and returns a trace of handler outcomes (completed, short-circuited, skipped, failed, or not reached).
    /// When a handler throws, the exception is recorded in the trace and re-thrown.
    /// </summary>
    public async ValueTask<HandlerPipelineTrace> InvokeTracedAsync(
        TContext context,
        CancellationToken cancellationToken = default)
    {
        var traceBuilder = new HandlerPipelineTraceBuilder(_registrations);
        var pipeline = BuildTracedPipeline(_registrations, traceBuilder, null);
        await pipeline(context, cancellationToken).ConfigureAwait(false);
        return traceBuilder.ToTrace();
    }

    /// <summary>
    /// Invokes the pipeline and returns a trace of handler outcomes, notifying
    /// <paramref name="exceptionObserver"/> when a handler throws. The exception
    /// is recorded in the trace and re-thrown after the observer is notified.
    /// </summary>
    public async ValueTask<HandlerPipelineTrace> InvokeTracedAsync(
        TContext context,
        IHandlerExceptionObserver<TContext>? exceptionObserver,
        CancellationToken cancellationToken = default)
    {
        if (exceptionObserver is null)
        {
            return await InvokeTracedAsync(context, cancellationToken).ConfigureAwait(false);
        }

        var traceBuilder = new HandlerPipelineTraceBuilder(_registrations);
        var pipeline = BuildTracedPipeline(_registrations, traceBuilder, exceptionObserver);
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
        HandlerPipelineTraceBuilder traceBuilder,
        IHandlerExceptionObserver<TContext>? exceptionObserver)
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

                    try
                    {
                        await registration.Handler
                            .InvokeAsync(context, tracedNext, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (traceBuilder.TryRecordFailure(index, ex, context, exceptionObserver))
                    {
                        throw;
                    }

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
                    bool guardPassed;
                    try
                    {
                        guardPassed = guard(context);
                    }
                    catch (Exception ex) when (traceBuilder.TryRecordFailure(index, ex, context, exceptionObserver))
                    {
                        throw;
                    }

                    if (!guardPassed)
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

                    try
                    {
                        await registration.Handler
                            .InvokeAsync(context, tracedNext, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (traceBuilder.TryRecordFailure(index, ex, context, exceptionObserver))
                    {
                        throw;
                    }

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
        private readonly Exception?[] _exceptions;

        public HandlerPipelineTraceBuilder(IReadOnlyList<HandlerPipelineRegistration<TContext>> registrations)
        {
            _names = new string[registrations.Count];
            _statuses = new HandlerPipelineStepStatus[registrations.Count];
            _exceptions = new Exception?[registrations.Count];

            for (var i = 0; i < registrations.Count; i++)
            {
                _names[i] = registrations[i].DisplayName;
                _statuses[i] = HandlerPipelineStepStatus.NotReached;
            }
        }

        public void Record(int index, HandlerPipelineStepStatus status) =>
            _statuses[index] = status;

        /// <summary>
        /// Records a handler failure at the given index, captures the exception,
        /// and notifies the observer (if provided). Returns <see langword="true"/>
        /// to allow the <c>when</c> filter to re-throw.
        /// <para>
        /// If a failure has already been recorded (by an inner handler), this
        /// method returns <see langword="false"/> so the <c>when</c> filter does
        /// not re-capture the propagating exception at an outer handler level.
        /// </para>
        /// </summary>
        public bool TryRecordFailure(
            int index,
            Exception exception,
            TContext context,
            IHandlerExceptionObserver<TContext>? observer)
        {
            // If a failure was already recorded by a deeper handler, don't
            // overwrite it — let the exception propagate without re-capturing.
            for (var i = 0; i < _statuses.Length; i++)
            {
                if (_statuses[i] == HandlerPipelineStepStatus.Failed)
                {
                    return false;
                }
            }

            _statuses[index] = HandlerPipelineStepStatus.Failed;
            _exceptions[index] = exception;

            // Mark all handlers after the failed one as NotReached (they already
            // are from initialization, but be explicit in case of re-entry).
            for (var i = index + 1; i < _statuses.Length; i++)
            {
                _statuses[i] = HandlerPipelineStepStatus.NotReached;
            }

            observer?.OnHandlerException(context, index, _names[index], exception);

            return true;
        }

        public HandlerPipelineTrace ToTrace()
        {
            var steps = new HandlerPipelineStep[_names.Length];

            for (var i = 0; i < _names.Length; i++)
            {
                steps[i] = new HandlerPipelineStep(i, _names[i], _statuses[i], _exceptions[i]);
            }

            return new HandlerPipelineTrace(steps);
        }
    }
}
