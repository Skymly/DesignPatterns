using System;
using Autofac;
using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac;

/// <summary>
/// Autofac extension methods for registering an <see cref="ICommandRouter"/>.
/// </summary>
public static class AutofacCommandRouterExtensions
{
    /// <summary>
    /// Registers an <see cref="ICommandRouter"/> built from a configure delegate that receives
    /// a <see cref="CommandRouterBuilder"/> and the resolving <see cref="ILifetimeScope"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pair with generated <c>{Command}CommandHandlerRegistry.RegisterAutofac</c> and
    /// <c>RegisterAll(CommandRouterBuilder, ILifetimeScope)</c>:
    /// </para>
    /// <code>
    /// var builder = new ContainerBuilder();
    /// PingCommandHandlerRegistry.RegisterAutofac(builder);
    /// builder.RegisterCommandRouter((routerBuilder, scope) =&gt;
    ///     PingCommandHandlerRegistry.RegisterAll(routerBuilder, scope));
    /// </code>
    /// <para>
    /// Call <c>RegisterAutofac</c> (or otherwise register handler types) on the
    /// <see cref="ContainerBuilder"/> before building so handlers are available for resolution.
    /// Generated <c>RegisterAutofac</c> registers concrete handlers with Autofac's default
    /// <c>InstancePerDependency</c> lifetime (Transient-equivalent).
    /// </para>
    /// <para>
    /// <strong>Lifetime pitfall (captive dependency):</strong> the default router sharing is
    /// <see cref="InstanceSharing.Shared"/> (singleton). Generated
    /// <c>RegisterAll(CommandRouterBuilder, ILifetimeScope)</c> resolves handlers once when the
    /// router is built and freezes them into the immutable map—the same capture shape as Event
    /// Aggregator <c>SubscribeAll(aggregator, lifetimeScope)</c> and MSDI
    /// <c>AddCommandRouter</c>. An InstancePerDependency registration therefore does
    /// <em>not</em> yield a new handler instance per <c>Send</c>; it only affects the instance
    /// captured at build time. Prefer InstancePerDependency (default via <c>RegisterAutofac</c>)
    /// or Shared handlers that are thread-safe and effectively immutable. Prefer matching router
    /// and handler lifetimes, or register the router with <see cref="InstanceSharing.None"/> when
    /// handlers must follow a shorter lifetime. Related vocabulary: DP060–DP062 / DP066
    /// captive-dependency diagnostics.
    /// </para>
    /// </remarks>
    /// <param name="builder">The Autofac container builder.</param>
    /// <param name="configure">
    /// A delegate that registers handlers onto a <see cref="CommandRouterBuilder"/> using the
    /// resolving <see cref="ILifetimeScope"/> (typically via generated
    /// <c>RegisterAll(builder, lifetimeScope)</c>).
    /// </param>
    /// <param name="sharing">
    /// The router instance sharing mode. Defaults to <see cref="InstanceSharing.Shared"/>
    /// (singleton).
    /// </param>
    /// <returns>The container builder for chaining.</returns>
    public static ContainerBuilder RegisterCommandRouter(
        this ContainerBuilder builder,
        Action<CommandRouterBuilder, ILifetimeScope> configure,
        InstanceSharing sharing = InstanceSharing.Shared)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var registration = builder.Register(ctx =>
            {
                var routerBuilder = new CommandRouterBuilder();
                configure(routerBuilder, ctx.Resolve<ILifetimeScope>());
                return routerBuilder.Build();
            })
            .As<ICommandRouter>()
            .IfNotRegistered(typeof(ICommandRouter));

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
