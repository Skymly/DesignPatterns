using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesignPatterns.Structural;

/// <summary>
/// Builds a decorated <typeparamref name="TService"/> by wrapping a core instance.
/// Decorators registered first are outermost (they receive calls first).
/// Supports both sync <see cref="IDecorator{TService}"/> and async
/// <see cref="IAsyncDecorator{TService}"/> decorators in the same stack.
/// </summary>
/// <typeparam name="TService">The service contract type.</typeparam>
public sealed class DecoratorStackBuilder<TService>
    where TService : class
{
    private readonly List<Func<TService, TService>> _decorators = new();
    private readonly List<Func<TService, CancellationToken, ValueTask<TService>>> _asyncDecorators = new();

    /// <summary>
    /// Registers a decorator type with a public parameterless constructor.
    /// </summary>
    public DecoratorStackBuilder<TService> Add<TDecorator>()
        where TDecorator : class, IDecorator<TService>, new()
    {
        return Add(new TDecorator());
    }

    /// <summary>
    /// Registers a decorator instance.
    /// </summary>
    public DecoratorStackBuilder<TService> Add(IDecorator<TService> decorator)
    {
        if (decorator is null)
        {
            throw new ArgumentNullException(nameof(decorator));
        }

        _decorators.Add(decorator.Decorate);
        return this;
    }

    /// <summary>
    /// Registers an async decorator instance.
    /// Async decorators are applied after all sync decorators during <see cref="BuildAsync"/>.
    /// </summary>
    public DecoratorStackBuilder<TService> Add(IAsyncDecorator<TService> decorator)
    {
        if (decorator is null)
        {
            throw new ArgumentNullException(nameof(decorator));
        }

        _asyncDecorators.Add((inner, ct) => decorator.DecorateAsync(inner, ct));
        return this;
    }

    /// <summary>
    /// Registers a decorator type when <paramref name="condition"/> evaluates to <see langword="true"/> at build time.
    /// </summary>
    public DecoratorStackBuilder<TService> Add<TDecorator>(Func<bool> condition)
        where TDecorator : class, IDecorator<TService>, new()
    {
        return Add(new TDecorator(), condition);
    }

    /// <summary>
    /// Registers a decorator instance when <paramref name="condition"/> evaluates to <see langword="true"/> at build time.
    /// </summary>
    public DecoratorStackBuilder<TService> Add(IDecorator<TService> decorator, Func<bool> condition)
    {
        if (decorator is null)
        {
            throw new ArgumentNullException(nameof(decorator));
        }

        if (condition is null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        _decorators.Add(inner => condition() ? decorator.Decorate(inner) : inner);
        return this;
    }

    /// <summary>
    /// Applies all registered sync decorators to <paramref name="core"/>.
    /// Returns <paramref name="core"/> unchanged when no decorators were registered.
    /// </summary>
    public TService Build(TService core)
    {
        if (core is null)
        {
            throw new ArgumentNullException(nameof(core));
        }

        var current = core;
        foreach (var apply in _decorators)
        {
            current = apply(current);
        }

        return current;
    }

    /// <summary>
    /// Applies all registered sync and async decorators to <paramref name="core"/>.
    /// Sync decorators are applied first, then async decorators in registration order.
    /// </summary>
    public async ValueTask<TService> BuildAsync(TService core, CancellationToken cancellationToken = default)
    {
        if (core is null)
        {
            throw new ArgumentNullException(nameof(core));
        }

        var current = core;
        foreach (var apply in _decorators)
        {
            current = apply(current);
        }

        foreach (var applyAsync in _asyncDecorators)
        {
            current = await applyAsync(current, cancellationToken);
        }

        return current;
    }
}
