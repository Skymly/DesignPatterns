using DesignPatterns.Creational;

namespace RegisterFactory.Sample;

[RegisterFactory<IProductFactory>("standard")]
public sealed class StandardProductFactory : IProductFactory
{
    public IProduct Create() => new StandardProduct();
}

[RegisterFactory<IProductFactory>("premium")]
public sealed class PremiumProductFactory : IProductFactory
{
    public IProduct Create() => new PremiumProduct();
}
