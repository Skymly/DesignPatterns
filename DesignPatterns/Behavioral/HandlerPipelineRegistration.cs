using System;

namespace DesignPatterns.Behavioral;

internal readonly struct HandlerPipelineRegistration<TContext>
{
    public HandlerPipelineRegistration(IHandler<TContext> handler, string displayName)
        : this(handler, displayName, guard: null)
    {
    }

    public HandlerPipelineRegistration(
        IHandler<TContext> handler,
        string displayName,
        Func<TContext, bool>? guard)
    {
        Handler = handler;
        DisplayName = displayName;
        Guard = guard;
    }

    public IHandler<TContext> Handler { get; }

    public string DisplayName { get; }

    public Func<TContext, bool>? Guard { get; }
}
