using DesignPatterns.Structural;

namespace DesignPatterns.Tests.Integration.Decorator;

public interface IIntegrationPaymentService
{
    string Pay(decimal amount);
}

public sealed class IntegrationPaymentService : IIntegrationPaymentService
{
    public string Pay(decimal amount) => $"paid:{amount}";
}

[Decorator<IIntegrationPaymentService>(10)]
public sealed class IntegrationOuterDecorator : IIntegrationPaymentService, IDecorator<IIntegrationPaymentService>
{
    public static string? LastSeen { get; set; }

    public IIntegrationPaymentService Decorate(IIntegrationPaymentService inner) => new Impl(inner);

    public string Pay(decimal amount) => throw new NotSupportedException();

    private sealed class Impl(IIntegrationPaymentService inner) : IIntegrationPaymentService
    {
        public string Pay(decimal amount)
        {
            LastSeen = "outer-before";
            var result = inner.Pay(amount);
            LastSeen = "outer-after";
            return result;
        }
    }
}

[Decorator<IIntegrationPaymentService>(20)]
public sealed class IntegrationInnerDecorator : IIntegrationPaymentService, IDecorator<IIntegrationPaymentService>
{
    public static string? LastSeen { get; set; }

    public IIntegrationPaymentService Decorate(IIntegrationPaymentService inner) => new Impl(inner);

    public string Pay(decimal amount) => throw new NotSupportedException();

    private sealed class Impl(IIntegrationPaymentService inner) : IIntegrationPaymentService
    {
        public string Pay(decimal amount)
        {
            LastSeen = "inner";
            return inner.Pay(amount);
        }
    }
}

[Decorator<IIntegrationPaymentService>(30)]
public sealed class IntegrationAsyncDecorator : IIntegrationPaymentService, IAsyncDecorator<IIntegrationPaymentService>
{
    public static bool WasInvoked { get; set; }

    public ValueTask<IIntegrationPaymentService> DecorateAsync(IIntegrationPaymentService inner, CancellationToken cancellationToken = default)
    {
        WasInvoked = true;
        return new ValueTask<IIntegrationPaymentService>(new Impl(inner));
    }

    public string Pay(decimal amount) => throw new NotSupportedException();

    private sealed class Impl(IIntegrationPaymentService inner) : IIntegrationPaymentService
    {
        public string Pay(decimal amount) => $"async:{inner.Pay(amount)}";
    }
}

public sealed class DecoratorStackIntegrationTests
{
    [Fact]
    public void GeneratedBuild_AppliesDecoratorsInOrder_OuterFirst()
    {
        IntegrationOuterDecorator.LastSeen = null;
        IntegrationInnerDecorator.LastSeen = null;

        var service = IntegrationPaymentServiceDecoratorStack.Build(new IntegrationPaymentService());

        Assert.Equal("paid:10", service.Pay(10m));
        Assert.Equal("outer-after", IntegrationOuterDecorator.LastSeen);
        Assert.Equal("inner", IntegrationInnerDecorator.LastSeen);
    }

    [Fact]
    public async Task GeneratedBuildAsync_AppliesSyncAndAsyncDecorators()
    {
        IntegrationAsyncDecorator.WasInvoked = false;

        var service = await IntegrationPaymentServiceDecoratorStack.BuildAsync(new IntegrationPaymentService());

        Assert.True(IntegrationAsyncDecorator.WasInvoked);
        Assert.Equal("async:paid:10", service.Pay(10m));
    }
}
