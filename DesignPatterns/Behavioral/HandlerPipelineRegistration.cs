namespace DesignPatterns.Behavioral;

internal readonly struct HandlerPipelineRegistration<TContext>
{
    public HandlerPipelineRegistration(IHandler<TContext> handler, string displayName)
    {
        Handler = handler;
        DisplayName = displayName;
    }

    public IHandler<TContext> Handler { get; }

    public string DisplayName { get; }
}
