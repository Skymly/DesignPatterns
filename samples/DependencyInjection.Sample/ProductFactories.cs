using DesignPatterns.Creational;

namespace DependencyInjection.Sample;

[RegisterFactory<IProductFactory>("standard")]
public sealed class StandardProductFactory : IProductFactory
{
    public string Create() => "Standard";
}

[RegisterFactory<IProductFactory>("premium")]
public sealed class PremiumProductFactory : IProductFactory
{
    public string Create() => "Premium";
}
