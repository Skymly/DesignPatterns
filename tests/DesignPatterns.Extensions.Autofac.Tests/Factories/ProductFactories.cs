using DesignPatterns.Creational;

namespace DesignPatterns.Extensions.Autofac.Tests.Factories;

public interface IProductFactory
{
    string Create();
}

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
