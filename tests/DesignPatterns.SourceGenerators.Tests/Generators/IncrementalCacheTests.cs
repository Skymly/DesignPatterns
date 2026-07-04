using DesignPatterns.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;

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

    // ───────────────────────────────────────────────────────────────────
    //  Incremental edit tests — verify that editing one file only
    //  regenerates the contracts in that file, while other contracts
    //  remain cached. This is the core incremental value of source
    //  generators in large projects.
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two strategy contracts in separate files. After adding a new
    /// strategy to one file, the transform for the modified file should
    /// be Modified/New, and the Collect/Combine stages should be Modified
    /// (because one input changed).
    /// <para>
    /// Note: the unmodified file's transform is also Modified because
    /// RegisterStrategy's Transform returns List&lt;KeyedRegistration&gt;
    /// (reference equality). Once LocationInfo + EquatableArray are applied
    /// (PR #216), the unmodified file's transform will be Unchanged. Until
    /// then, this test verifies that the Combine stage correctly reflects
    /// the edit.
    /// </para>
    /// </summary>
    [Fact]
    public void RegisterStrategy_EditOneFile_CombineReflectsChange()
    {
        var shippingSource = """
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

        var paymentSource = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public interface IPaymentStrategy
            {
                void Pay(decimal amount);
            }

            [RegisterStrategy("credit", typeof(IPaymentStrategy))]
            public partial class CreditPayment : IPaymentStrategy
            {
                public void Pay(decimal amount) { }
            }
            """;

        // Modified shipping source: add a second strategy.
        var modifiedShippingSource = """
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

            [RegisterStrategy("standard", typeof(IShippingStrategy))]
            public partial class StandardShipping : IShippingStrategy
            {
                public decimal Calculate(decimal weight) => weight * 1m;
            }
            """;

        var secondResult = SourceGeneratorTestContext.RunIncrementalEdit<RegisterStrategyGenerator>(
            new[] { ("Shipping.cs", shippingSource), ("Payment.cs", paymentSource) },
            "Shipping.cs",
            modifiedShippingSource);

        // The Combine stage should be Modified (one input changed).
        // StrategyCollect is not tracked (no WithTrackingName on Collect),
        // but StrategyCombine covers the Collect+Combine aggregation.
        var combineReasons = SourceGeneratorTestContext.GetTrackedStepReasons(
            secondResult, TrackingNames.StrategyCombine);
        Assert.NotEmpty(combineReasons);
        Assert.All(combineReasons, reason =>
            Assert.True(
                reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
                $"StrategyCombine expected Modified or New, but was {reason}."));

        // The transform stage should reflect the edit: at least one output
        // should be Modified or New (the added StandardShipping strategy).
        var transformReasons = SourceGeneratorTestContext.GetTrackedStepReasons(
            secondResult, TrackingNames.StrategyNonGenericTransform);
        Assert.NotEmpty(transformReasons);
        Assert.Contains(transformReasons, reason =>
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);
    }

    /// <summary>
    /// Two singleton targets in separate files. After modifying one file
    /// (adding a second singleton), the transform stage should reflect
    /// the change — at least one output should be Modified or Added.
    /// GenerateSingleton has no Collect stage (per-item output), so we
    /// check the transform tracking name directly.
    /// </summary>
    [Fact]
    public void GenerateSingleton_EditOneFile_TransformReflectsChange()
    {
        var serviceSource = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public partial class MyService;
            """;

        var repoSource = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public partial class MyRepository;
            """;

        // Modified service source: add a second singleton.
        var modifiedServiceSource = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateSingleton]
            public partial class MyService;

            [GenerateSingleton]
            public partial class MyCache;
            """;

        var secondResult = SourceGeneratorTestContext.RunIncrementalEdit<GenerateSingletonGenerator>(
            new[] { ("Service.cs", serviceSource), ("Repo.cs", repoSource) },
            "Service.cs",
            modifiedServiceSource);

        // The transform stage should have at least one Modified or Added
        // output (the new MyCache singleton). The existing MyService and
        // MyRepository may be Cached if their syntax nodes are unchanged.
        var transformReasons = SourceGeneratorTestContext.GetTrackedStepReasons(
            secondResult, TrackingNames.SingletonTransform);
        Assert.NotEmpty(transformReasons);
        Assert.Contains(transformReasons, reason =>
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);

        // The unmodified file's transform should be Cached or Unchanged —
        // Repo.cs was not edited, so MyRepository's transform output should
        // be unchanged. (Cached = not re-executed; Unchanged = re-executed
        // but produced identical output.)
        Assert.Contains(transformReasons, reason =>
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
    }

    /// <summary>
    /// Two state machines in separate files. After modifying one file
    /// (adding a new transition), the Collect stage should be Modified.
    /// </summary>
    [Fact]
    public void StateTransition_EditOneFile_CollectIsModified()
    {
        var orderSource = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted }
            public enum OrderTrigger { Submit }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            public static partial class OrderStatusMachine;
            """;

        var paymentSource = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum PaymentStatus { Pending, Paid }
            public enum PaymentTrigger { Confirm }

            [StateMachine(typeof(PaymentStatus), typeof(PaymentTrigger), Initial = PaymentStatus.Pending)]
            [Transition(PaymentStatus.Pending, PaymentTrigger.Confirm, PaymentStatus.Paid)]
            public static partial class PaymentStatusMachine;
            """;

        // Modified order source: add a new transition.
        var modifiedOrderSource = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public enum OrderStatus { Draft, Submitted, Cancelled }
            public enum OrderTrigger { Submit, Cancel }

            [StateMachine(typeof(OrderStatus), typeof(OrderTrigger), Initial = OrderStatus.Draft)]
            [Transition(OrderStatus.Draft, OrderTrigger.Submit, OrderStatus.Submitted)]
            [Transition(OrderStatus.Draft, OrderTrigger.Cancel, OrderStatus.Cancelled)]
            public static partial class OrderStatusMachine;
            """;

        var secondResult = SourceGeneratorTestContext.RunIncrementalEdit<StateTransitionGenerator>(
            new[] { ("OrderMachine.cs", orderSource), ("PaymentMachine.cs", paymentSource) },
            "OrderMachine.cs",
            modifiedOrderSource);

        // The Combine stage should be Modified (one input changed).
        // StateMachineCollect is not tracked (no WithTrackingName on Collect),
        // but StateMachineCombine covers the Collect+Combine aggregation.
        var combineReasons = SourceGeneratorTestContext.GetTrackedStepReasons(
            secondResult, TrackingNames.StateMachineCombine);
        Assert.NotEmpty(combineReasons);
        Assert.All(combineReasons, reason =>
            Assert.True(
                reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
                $"StateMachineCombine expected Modified or New, but was {reason}."));

        // The transform stage should reflect the edit.
        var transformReasons = SourceGeneratorTestContext.GetTrackedStepReasons(
            secondResult, TrackingNames.StateMachineTransform);
        Assert.NotEmpty(transformReasons);
        Assert.Contains(transformReasons, reason =>
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);

        // The unmodified file's transform should be Cached or Unchanged —
        // PaymentMachine.cs was not edited, so PaymentStatusMachine's
        // transform should be unchanged. (Cached = not re-executed;
        // Unchanged = re-executed but produced identical output.)
        Assert.Contains(transformReasons, reason =>
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
    }
}
