namespace DesignPatterns.Extensions.AppSettings.Tests;

internal interface IProviderStrategy
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
