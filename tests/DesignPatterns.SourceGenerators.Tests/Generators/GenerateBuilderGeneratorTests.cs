using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

/// <summary>
/// Seam: generated <c>{Holder}Builder</c> public API and schema diagnostics (issue #290).
/// </summary>
public sealed class GenerateBuilderGeneratorTests
{
    [Fact]
    public Task GeneratesTypedStepBuilderWithGatedBuild()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class HttpRequestSchema
            {
                [BuilderStep]
                public static void WithUrl(string url)
                {
                }

                [BuilderStep]
                public static void WithMethod(string method)
                {
                }

                [BuilderStep(Required = false)]
                public static void WithHeader(string header)
                {
                }

                [BuilderStep(Required = false, MutexGroup = "Auth")]
                public static void WithBearerToken(string token)
                {
                }

                [BuilderStep(Required = false, MutexGroup = "Auth")]
                public static void WithBasicAuth(string credentials)
                {
                }

                [BuilderAssemble]
                public static string Assemble(
                    string url,
                    string method,
                    string? header,
                    string? bearerToken,
                    string? basicAuth) =>
                    method + " " + url;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("HttpRequestSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp079WhenAssembleIsMissing()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class MissingAssembleSchema
            {
                [BuilderStep]
                public static void WithName(string name)
                {
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("MissingAssembleSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp078WhenRequiredStepsExceedCap()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class TooManyRequiredSchema
            {
                [BuilderStep] public static void WithA(string v) { }
                [BuilderStep] public static void WithB(string v) { }
                [BuilderStep] public static void WithC(string v) { }
                [BuilderStep] public static void WithD(string v) { }
                [BuilderStep] public static void WithE(string v) { }
                [BuilderStep] public static void WithF(string v) { }
                [BuilderStep] public static void WithG(string v) { }
                [BuilderStep] public static void WithH(string v) { }
                [BuilderStep] public static void WithI(string v) { }

                [BuilderStep(Required = false)]
                public static void WithOptional(string v) { }

                [BuilderAssemble]
                public static string Assemble(
                    string a, string b, string c, string d, string e, string f, string g, string h, string i, string? optional) => a;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("TooManyRequiredSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp080WhenAssembleParameterDoesNotBind()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class MismatchSchema
            {
                [BuilderStep]
                public static void WithUrl(string url)
                {
                }

                [BuilderAssemble]
                public static string Assemble(string url, string unknown) => url;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("MismatchSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp081WhenTwoRequiredStepsShareMutexGroup()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class MutexSchema
            {
                [BuilderStep(MutexGroup = "Auth")]
                public static void WithBearerToken(string token)
                {
                }

                [BuilderStep(MutexGroup = "Auth")]
                public static void WithBasicAuth(string credentials)
                {
                }

                [BuilderAssemble]
                public static string Assemble(string bearerToken, string basicAuth) => bearerToken;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("MutexSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp082WhenAfterBeforeConstraintsCycle()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class CycleSchema
            {
                [BuilderStep(After = nameof(WithB))]
                public static void WithA(string a)
                {
                }

                [BuilderStep(After = nameof(WithA))]
                public static void WithB(string b)
                {
                }

                [BuilderAssemble]
                public static string Assemble(string a, string b) => a;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("CycleSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp083WhenStepNameIsDuplicated()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class DuplicateStepSchema
            {
                [BuilderStep]
                public static void WithUrl(string url)
                {
                }

                [BuilderStep]
                public static void Url(string url)
                {
                }

                [BuilderAssemble]
                public static string Assemble(string url) => url;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("DuplicateStepSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp084WhenAfterReferencesUnknownStep()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class UnknownRefSchema
            {
                [BuilderStep(After = "MissingStep")]
                public static void WithUrl(string url)
                {
                }

                [BuilderAssemble]
                public static string Assemble(string url) => url;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("UnknownRefSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp085WhenHolderIsGeneric()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class GenericHolder<T>
            {
                [BuilderStep]
                public static void WithValue(T value)
                {
                }

                [BuilderAssemble]
                public static string Assemble(T value) => value!.ToString()!;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("GenericHolder.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp085WhenHolderIsPrivateNested()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public static class Outer
            {
                [GenerateBuilder]
                private static class PrivateHolder
                {
                    [BuilderStep]
                    public static void WithName(string name)
                    {
                    }

                    [BuilderAssemble]
                    public static string Assemble(string name) => name;
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("PrivateHolder.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp086WhenAssembleReturnsVoid()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            [GenerateBuilder]
            public static class VoidAssembleSchema
            {
                [BuilderStep]
                public static void WithName(string name)
                {
                }

                [BuilderAssemble]
                public static void Assemble(string name)
                {
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<GenerateBuilderGenerator>(
            ("VoidAssembleSchema.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
