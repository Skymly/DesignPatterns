using DesignPatterns.Structural;

namespace DesignPatterns.Tests.Structural;

public sealed class DecoratorStackBuilderTests
{
    private interface ICounter
    {
        int Invoke();
    }

    private sealed class CoreCounter : ICounter
    {
        public int Invoke() => 1;
    }

    private sealed class AddOneDecorator : IDecorator<ICounter>
    {
        public ICounter Decorate(ICounter inner) => new Impl(inner);

        private sealed class Impl(ICounter inner) : ICounter
        {
            public int Invoke() => inner.Invoke() + 1;
        }
    }

    private sealed class MultiplyDecorator : IDecorator<ICounter>
    {
        public ICounter Decorate(ICounter inner) => new Impl(inner);

        private sealed class Impl(ICounter inner) : ICounter
        {
            public int Invoke() => inner.Invoke() * 10;
        }
    }

    private sealed class TrackingDecorator : IDecorator<ICounter>
    {
        public static int DecorateCallCount { get; set; }

        public ICounter Decorate(ICounter inner)
        {
            DecorateCallCount++;
            return inner;
        }
    }

    [Fact]
    public void Build_WithNoDecorators_ReturnsSameCoreReference()
    {
        var core = new CoreCounter();

        var result = new DecoratorStackBuilder<ICounter>().Build(core);

        Assert.Same(core, result);
    }

    [Fact]
    public void Build_AppliesDecoratorsInRegistrationOrder_OuterFirst()
    {
        var result = new DecoratorStackBuilder<ICounter>()
            .Add<AddOneDecorator>()
            .Add<MultiplyDecorator>()
            .Build(new CoreCounter());

        Assert.Equal(20, result.Invoke());
    }

    [Fact]
    public void Add_WithInstance_WrapsCore()
    {
        var result = new DecoratorStackBuilder<ICounter>()
            .Add(new AddOneDecorator())
            .Build(new CoreCounter());

        Assert.Equal(2, result.Invoke());
    }

    [Fact]
    public void Add_Generic_UsesParameterlessConstructor()
    {
        TrackingDecorator.DecorateCallCount = 0;

        new DecoratorStackBuilder<ICounter>()
            .Add<TrackingDecorator>()
            .Build(new CoreCounter());

        Assert.Equal(1, TrackingDecorator.DecorateCallCount);
    }

    [Fact]
    public void Build_NullCore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DecoratorStackBuilder<ICounter>().Build(null!));
    }

    [Fact]
    public void Add_NullDecorator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DecoratorStackBuilder<ICounter>().Add(null!));
    }
}
