using System.Threading;
using System.Threading.Tasks;
using DesignPatterns.Creational;

namespace DesignPatterns.Extensions.Autofac.Tests.Factories;

public interface IAsyncProductFactory
{
    string Create();
}

[RegisterFactory<IAsyncProductFactory>("standard")]
public sealed class StandardAsyncProductFactory : IAsyncProductFactory, IAsyncFactory<IAsyncProductFactory>
{
    public string Create() => "Standard";

    public ValueTask<IAsyncProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
        new(new StandardAsyncProductFactory());
}

[RegisterFactory<IAsyncProductFactory>("premium")]
public sealed class PremiumAsyncProductFactory : IAsyncProductFactory, IAsyncFactory<IAsyncProductFactory>
{
    public string Create() => "Premium";

    public ValueTask<IAsyncProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
        new(new PremiumAsyncProductFactory());
}

public interface IPooledProductFactory
{
    string Create();
}

[RegisterFactory<IPooledProductFactory>("standard", PoolSize = 4)]
public sealed class StandardPooledProductFactory : IPooledProductFactory, IAsyncFactory<IPooledProductFactory>
{
    public string Create() => "Standard";

    public ValueTask<IPooledProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
        new(new StandardPooledProductFactory());
}
