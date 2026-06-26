using System;
using Autofac;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac;

/// <summary>
/// Autofac extension methods for registering transition tables and state machines.
/// </summary>
public static class AutofacStateTransitionExtensions
{
    /// <summary>
    /// Registers a pre-built <see cref="ITransitionTable{TState,TTrigger}"/> instance as a singleton.
    /// Transition tables are stateless and immutable.
    /// </summary>
    /// <typeparam name="TState">The state enum type.</typeparam>
    /// <typeparam name="TTrigger">The trigger enum type.</typeparam>
    /// <param name="builder">The Autofac container builder.</param>
    /// <param name="table">The transition table instance to register.</param>
    /// <returns>The container builder for chaining.</returns>
    public static ContainerBuilder RegisterTransitionTable<TState, TTrigger>(
        this ContainerBuilder builder,
        ITransitionTable<TState, TTrigger> table)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        builder.Register(_ => table)
            .As<ITransitionTable<TState, TTrigger>>()
            .SingleInstance();

        return builder;
    }

    /// <summary>
    /// Registers an <see cref="IStateMachine{TState,TTrigger}"/> that wraps the
    /// <see cref="ITransitionTable{TState,TTrigger}"/> resolved from the container.
    /// The table must be registered separately (e.g. via generated <c>RegisterAutofac</c>
    /// or <see cref="RegisterTransitionTable{TState,TTrigger}"/>).
    /// </summary>
    /// <typeparam name="TState">The state enum type.</typeparam>
    /// <typeparam name="TTrigger">The trigger enum type.</typeparam>
    /// <param name="builder">The Autofac container builder.</param>
    /// <param name="sharing">The instance sharing mode. Defaults to <see cref="InstanceSharing.Shared"/>
    /// (singleton). Use <see cref="InstanceSharing.None"/> when each consumer needs its own state tracking.</param>
    /// <returns>The container builder for chaining.</returns>
    public static ContainerBuilder RegisterStateMachine<TState, TTrigger>(
        this ContainerBuilder builder,
        InstanceSharing sharing = InstanceSharing.Shared)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var registration = builder.Register(
            ctx => new StateMachine<TState, TTrigger>(
                ctx.Resolve<ITransitionTable<TState, TTrigger>>()))
            .As<IStateMachine<TState, TTrigger>>();

        if (sharing == InstanceSharing.Shared)
        {
            registration.SingleInstance();
        }
        else
        {
            registration.InstancePerDependency();
        }

        return builder;
    }
}
