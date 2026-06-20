using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

/// <summary>
/// Verifies that incremental generator pipeline stages return Cached or Unchanged
/// on the second run when the input compilation is identical. This validates the
/// P0 refactoring (value-equatable models) and P2 (WithTrackingName instrumentation).
/// </summary>
public sealed class IncrementalCacheTests
{
    [Fact]
    public void GenerateSingletonCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public partial class MyService;
            """;

        SourceGeneratorTestContext.AssertCacheHit<GenerateSingletonGenerator>(
            ("MyService.cs", source));
    }

    [Fact]
    public void RegisterFactoryCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IWidget
            {
                void Render();
            }

            [RegisterFactory("default", typeof(IWidget))]
            public partial class DefaultWidget : IWidget
            {
                public void Render() { }
            }
            """;

        SourceGeneratorTestContext.AssertCacheHit<RegisterFactoryGenerator>(
            ("Widget.cs", source));
    }

    [Fact]
    public void RegisterStrategyCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IShippingStrategy
            {
                decimal Calculate(decimal weight);
            }

            [RegisterStrategy("express", typeof(IShippingStrategy))]
            public partial class ExpressShipping : IShippingStrategy
            {
                public decimal Calculate(decimal weight) => weight * 2m;
            }
            """;

        SourceGeneratorTestContext.AssertCacheHit<RegisterStrategyGenerator>(
            ("Shipping.cs", source));
    }

    [Fact]
    public void HandlerOrderCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IRequest { }

            public interface IHandler<TRequest>
            {
                void Handle(TRequest request);
            }

            [HandlerOrder(1, typeof(IRequest))]
            public partial class FirstHandler : IHandler<IRequest>
            {
                public void Handle(IRequest request) { }
            }
            """;

        SourceGeneratorTestContext.AssertCacheHit<HandlerOrderGenerator>(
            ("Handler.cs", source));
    }

    [Fact]
    public void CompositePartCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IShape
            {
                void Draw();
            }

            public interface ICompositeBuildable<T>
            {
            }

            [CompositePart("circle", typeof(IShape))]
            public partial class Circle : IShape, ICompositeBuildable<IShape>
            {
                public void Draw() { }
            }
            """;

        SourceGeneratorTestContext.AssertCacheHit<CompositePartGenerator>(
            ("Shape.cs", source));
    }

    [Fact]
    public void DecoratorCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Structural;

            namespace TestAssembly;

            public interface IService
            {
                void Execute();
            }

            public interface IDecorator<TService>
            {
                TService Inner { get; set; }
            }

            [Decorator(1, typeof(IService))]
            public partial class LoggingDecorator : IService, IDecorator<IService>
            {
                public IService Inner { get; set; }
                public void Execute() { }
            }
            """;

        SourceGeneratorTestContext.AssertCacheHit<DecoratorGenerator>(
            ("Decorator.cs", source));
    }

    [Fact]
    public void StateTransitionCachesOnSecondRun()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;
            """;

        SourceGeneratorTestContext.AssertCacheHit<StateTransitionGenerator>(
            ("OrderMachine.cs", source));
    }
}
