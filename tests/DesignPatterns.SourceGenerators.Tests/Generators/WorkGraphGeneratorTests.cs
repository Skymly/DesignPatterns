using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

/// <summary>
/// Seam: generated <c>{Holder}WorkStepKeys</c> / <c>{Holder}WorkGraph.Create</c>
/// and Work Graph diagnostics DP087–DP092 (issue #311).
/// </summary>
public sealed class WorkGraphGeneratorTests
{
    [Fact]
    public Task GeneratesKeysAndCreateFacadeForDiamondGraph()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class PrepContext
            {
                public string Principal { get; set; } = "";
            }

            [WorkGraph<PrepContext>]
            public static class RequestPrep
            {
            }

            [WorkStep(typeof(RequestPrep), Id = "auth")]
            public sealed class AuthStep : IWorkStep<PrepContext>
            {
                public ValueTask ExecuteAsync(PrepContext context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(RequestPrep), Id = "load-config")]
            public sealed class LoadConfigStep : IWorkStep<PrepContext>
            {
                public ValueTask ExecuteAsync(PrepContext context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(RequestPrep), Id = "build-principal", DependsOn = new[] { "auth", "load-config" })]
            public sealed class BuildPrincipalStep : IWorkStep<PrepContext>
            {
                public ValueTask ExecuteAsync(PrepContext context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(RequestPrep), Id = "authorize", DependsOn = new[] { "build-principal" })]
            public sealed class AuthorizeStep : IWorkStep<PrepContext>
            {
                public ValueTask ExecuteAsync(PrepContext context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("RequestPrep.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesKeysAndCreateFacadeForNonGenericWorkGraphAttribute()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph(typeof(Ctx))]
            public static class SimpleGraph
            {
            }

            [WorkStep(typeof(SimpleGraph), Id = "only")]
            public sealed class OnlyStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("SimpleGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp089WhenStepIdIsDuplicated()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class DupGraph { }

            [WorkStep(typeof(DupGraph), Id = "same")]
            public sealed class First : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(DupGraph), Id = "same")]
            public sealed class Second : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("DupGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp090WhenStepDependsOnItself()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class SelfGraph { }

            [WorkStep(typeof(SelfGraph), Id = "loop", DependsOn = new[] { "loop" })]
            public sealed class LoopStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("SelfGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp088WhenDependsOnIsUnknown()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class UnknownDepGraph { }

            [WorkStep(typeof(UnknownDepGraph), Id = "a", DependsOn = new[] { "missing" })]
            public sealed class AStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("UnknownDepGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp087WhenDependsOnFormsACycle()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class CycleGraph { }

            [WorkStep(typeof(CycleGraph), Id = "a", DependsOn = new[] { "b" })]
            public sealed class AStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(CycleGraph), Id = "b", DependsOn = new[] { "a" })]
            public sealed class BStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("CycleGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp091WhenStepIsUnreachableFromRoots()
    {
        // Root R is reachable; A↔B form a cycle and are unreachable from R.
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class OrphanGraph { }

            [WorkStep(typeof(OrphanGraph), Id = "root")]
            public sealed class RootStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(OrphanGraph), Id = "a", DependsOn = new[] { "b" })]
            public sealed class AStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(OrphanGraph), Id = "b", DependsOn = new[] { "a" })]
            public sealed class BStep : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("OrphanGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp092WhenStepDoesNotImplementIWorkStep()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class MismatchGraph { }

            [WorkStep(typeof(MismatchGraph), Id = "bad")]
            public sealed class NotAStep
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("MismatchGraph.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task EmitsCreateThatRejectsEmptyCatalogAtRuntime()
    {
        const string source = """
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class EmptyGraph
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("EmptyGraph.cs", source));

        Assert.Empty(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task MultiRootGraphDoesNotWarnUnreachable()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Behavioral;

            namespace TestAssembly;

            public sealed class Ctx { }

            [WorkGraph<Ctx>]
            public static class MultiRoot { }

            [WorkStep(typeof(MultiRoot), Id = "auth")]
            public sealed class Auth : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(MultiRoot), Id = "config")]
            public sealed class Config : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }

            [WorkStep(typeof(MultiRoot), Id = "join", DependsOn = new[] { "auth", "config" })]
            public sealed class Join : IWorkStep<Ctx>
            {
                public ValueTask ExecuteAsync(Ctx context, CancellationToken cancellationToken = default) => default;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<WorkGraphGenerator>(
            ("MultiRoot.cs", source));

        Assert.Empty(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }
}
