using System;
using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DesignPatterns.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DesignPatterns types with <see cref="IServiceCollection"/>.
/// </summary>
public static class DesignPatternsServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IStrategyRegistry{TKey,TStrategy}"/> built by the provided configuration delegate.
    /// </summary>
    /// <typeparam name="TKey">The strategy key type.</typeparam>
    /// <typeparam name="TStrategy">The strategy implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the <see cref="StrategyRegistryBuilder{TKey,TStrategy}"/>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStrategyRegistry<TKey, TStrategy>(
        this IServiceCollection services,
        Action<StrategyRegistryBuilder<TKey, TStrategy>> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.TryAdd(new ServiceDescriptor(
            typeof(IStrategyRegistry<TKey, TStrategy>),
            _ =>
            {
                var builder = new StrategyRegistryBuilder<TKey, TStrategy>();
                configure(builder);
                return builder.Build();
            },
            lifetime));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IFactoryRegistry{TKey,TProduct}"/> built by the provided configuration delegate.
    /// </summary>
    /// <typeparam name="TKey">The factory key type.</typeparam>
    /// <typeparam name="TProduct">The product type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the <see cref="FactoryRegistryBuilder{TKey,TProduct}"/>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFactoryRegistry<TKey, TProduct>(
        this IServiceCollection services,
        Action<FactoryRegistryBuilder<TKey, TProduct>> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.TryAdd(new ServiceDescriptor(
            typeof(IFactoryRegistry<TKey, TProduct>),
            _ =>
            {
                var builder = new FactoryRegistryBuilder<TKey, TProduct>();
                configure(builder);
                return builder.Build();
            },
            lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="HandlerPipeline{TContext}"/> built by the provided configuration delegate.
    /// </summary>
    /// <typeparam name="TContext">The context type flowing through the pipeline.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure the <see cref="HandlerPipelineBuilder{TContext}"/>.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHandlerPipeline<TContext>(
        this IServiceCollection services,
        Action<HandlerPipelineBuilder<TContext>> configure,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.TryAdd(new ServiceDescriptor(
            typeof(HandlerPipeline<TContext>),
            _ =>
            {
                var builder = new HandlerPipelineBuilder<TContext>();
                configure(builder);
                return builder.Build();
            },
            lifetime));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IEventAggregator"/> backed by <see cref="EventAggregator"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventAggregator(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAdd(new ServiceDescriptor(
            typeof(IEventAggregator),
            _ => new EventAggregator(),
            lifetime));

        return services;
    }
}
