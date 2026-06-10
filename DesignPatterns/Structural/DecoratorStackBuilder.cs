using System;
using System.Collections.Generic;

namespace DesignPatterns.Structural;

/// <summary>
/// Builds a decorated <typeparamref name="TService"/> by wrapping a core instance.
/// Decorators registered first are outermost (they receive calls first).
/// </summary>
/// <typeparam name="TService">The service contract type.</typeparam>
public sealed class DecoratorStackBuilder<TService>
    where TService : class
{
    private readonly List<Func<TService, TService>> _decorators = new();

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
    /// Applies all registered decorators to <paramref name="core"/>.
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
}
