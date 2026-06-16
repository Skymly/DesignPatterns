namespace DesignPatterns.Extensions.Configuration.Tests;

public interface IProviderStrategy
{
    string Name { get; }
}

internal sealed class AlphaProvider : IProviderStrategy
{
    public string Name => "alpha";
}

internal sealed class BetaProvider : IProviderStrategy
{
    public string Name => "beta";
}
