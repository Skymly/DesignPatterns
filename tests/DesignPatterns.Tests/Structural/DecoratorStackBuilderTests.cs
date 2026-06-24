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
            new DecoratorStackBuilder<ICounter>().Add((IDecorator<ICounter>)null!));
    }

    [Fact]
    public void Add_WithConditionTrue_AppliesDecorator()
    {
        var result = new DecoratorStackBuilder<ICounter>()
            .Add(new AddOneDecorator(), () => true)
            .Build(new CoreCounter());

        Assert.Equal(2, result.Invoke());
    }

    [Fact]
    public void Add_WithConditionFalse_SkipsDecorator()
    {
        TrackingDecorator.DecorateCallCount = 0;

        var core = new CoreCounter();
        var result = new DecoratorStackBuilder<ICounter>()
            .Add(new TrackingDecorator(), () => false)
            .Build(core);

        Assert.Same(core, result);
        Assert.Equal(0, TrackingDecorator.DecorateCallCount);
    }

    private sealed class AsyncAddOneDecorator : IAsyncDecorator<ICounter>
    {
        public ValueTask<ICounter> DecorateAsync(ICounter inner, CancellationToken cancellationToken = default)
        {
            return new ValueTask<ICounter>(new Impl(inner));
        }

        private sealed class Impl(ICounter inner) : ICounter
        {
            public int Invoke() => inner.Invoke() + 1;
        }
    }

    private sealed class AsyncMultiplyDecorator : IAsyncDecorator<ICounter>
    {
        public ValueTask<ICounter> DecorateAsync(ICounter inner, CancellationToken cancellationToken = default)
        {
            return new ValueTask<ICounter>(new Impl(inner));
        }

        private sealed class Impl(ICounter inner) : ICounter
        {
            public int Invoke() => inner.Invoke() * 10;
        }
    }

    private sealed class AsyncTrackingDecorator : IAsyncDecorator<ICounter>
    {
        public static int DecorateCallCount { get; set; }

        public ValueTask<ICounter> DecorateAsync(ICounter inner, CancellationToken cancellationToken = default)
        {
            DecorateCallCount++;
            return new ValueTask<ICounter>(inner);
        }
    }

    [Fact]
    public void Add_WithCondition_ReEvaluatesOnEachBuild()
    {
        var enabled = false;

        var builder = new DecoratorStackBuilder<ICounter>()
            .Add(new AddOneDecorator(), () => enabled);

        var first = builder.Build(new CoreCounter());
        Assert.Equal(1, first.Invoke());

        enabled = true;
        var second = builder.Build(new CoreCounter());
        Assert.Equal(2, second.Invoke());
    }

    [Fact]
    public void Add_WithConditionNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DecoratorStackBuilder<ICounter>().Add(new AddOneDecorator(), null!));
    }

    [Fact]
    public void Add_WithConditionGeneric_UsesParameterlessConstructor()
    {
        TrackingDecorator.DecorateCallCount = 0;

        new DecoratorStackBuilder<ICounter>()
            .Add<TrackingDecorator>(() => true)
            .Build(new CoreCounter());

        Assert.Equal(1, TrackingDecorator.DecorateCallCount);
    }

    [Fact]
    public void Build_MixedConditionalAndUnconditional_PreservesRegistrationOrder()
    {
        var result = new DecoratorStackBuilder<ICounter>()
            .Add<AddOneDecorator>()
            .Add(new MultiplyDecorator(), () => false)
            .Add<MultiplyDecorator>()
            .Build(new CoreCounter());

        Assert.Equal(20, result.Invoke());
    }

    [Fact]
    public async Task BuildAsync_WithNoDecorators_ReturnsSameCoreReference()
    {
        var core = new CoreCounter();

        var result = await new DecoratorStackBuilder<ICounter>().BuildAsync(core);

        Assert.Same(core, result);
    }

    [Fact]
    public async Task BuildAsync_WithSyncDecoratorsOnly_AppliesDecorators()
    {
        var result = await new DecoratorStackBuilder<ICounter>()
            .Add<AddOneDecorator>()
            .Add<MultiplyDecorator>()
            .BuildAsync(new CoreCounter());

        Assert.Equal(20, result.Invoke());
    }

    [Fact]
    public async Task BuildAsync_WithAsyncDecoratorsOnly_AppliesDecorators()
    {
        var result = await new DecoratorStackBuilder<ICounter>()
            .Add(new AsyncAddOneDecorator())
            .Add(new AsyncMultiplyDecorator())
            .BuildAsync(new CoreCounter());

        Assert.Equal(20, result.Invoke());
    }

    [Fact]
    public async Task BuildAsync_WithMixedSyncAndAsync_AppliesSyncThenAsync()
    {
        // sync AddOne first (outer), then async Multiply (inner)
        // result = (1 + 1) * 10 = 20
        var result = await new DecoratorStackBuilder<ICounter>()
            .Add<AddOneDecorator>()
            .Add(new AsyncMultiplyDecorator())
            .BuildAsync(new CoreCounter());

        Assert.Equal(20, result.Invoke());
    }

    [Fact]
    public async Task BuildAsync_NullCore_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await new DecoratorStackBuilder<ICounter>().BuildAsync(null!));
    }

    [Fact]
    public void Add_NullAsyncDecorator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DecoratorStackBuilder<ICounter>().Add((IAsyncDecorator<ICounter>)null!));
    }

    [Fact]
    public async Task BuildAsync_AsyncDecoratorIsInvoked()
    {
        AsyncTrackingDecorator.DecorateCallCount = 0;

        await new DecoratorStackBuilder<ICounter>()
            .Add(new AsyncTrackingDecorator())
            .BuildAsync(new CoreCounter());

        Assert.Equal(1, AsyncTrackingDecorator.DecorateCallCount);
    }
}
