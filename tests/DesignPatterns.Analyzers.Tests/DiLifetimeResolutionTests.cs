using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Analyzers.Di;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Analyzers.Tests;

public sealed class DiLifetimeResolutionTests
{
    [Theory]
    [InlineData("ServiceLifetime.Singleton", 0)]
    [InlineData("ServiceLifetime.Scoped", 1)]
    [InlineData("ServiceLifetime.Transient", 2)]
    [InlineData("(ServiceLifetime)0", 0)]
    [InlineData("(ServiceLifetime)1", 1)]
    [InlineData("(ServiceLifetime)2", 2)]
    public void TryResolve_returns_lifetime_for_constant_expressions(string expression, int expected)
    {
        var (model, expr) = CompileFieldInitializer(expression);

        var actual = LifetimeResolution.TryResolve(expr, model);

        Assert.Equal((Lifetime)expected, actual);
    }

    [Theory]
    [InlineData("(ServiceLifetime)3")]
    [InlineData("lifetimeVariable")]
    public void TryResolve_returns_null_for_non_constant_or_unknown_values(string expression)
    {
        var source = $$"""
            using Microsoft.Extensions.DependencyInjection;

            class C
            {
                static ServiceLifetime lifetimeVariable = ServiceLifetime.Scoped;
                object _ = {{expression}};
            }
            """;
        var (model, expr) = CompileAndGetFieldInitializer(source);

        var actual = LifetimeResolution.TryResolve(expr, model);

        Assert.Null(actual);
    }

    [Fact]
    public void TryResolveArgument_reads_named_argument()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            static class Holder
            {
                public static void Register(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                {
                }

                public static void Call(IServiceCollection services)
                {
                    Register(services, implementationLifetime: ServiceLifetime.Transient);
                }
            }
            """;

        var (invocation, parameter, model) = CompileRegisterCall(source, "implementationLifetime");

        var actual = LifetimeResolution.TryResolveArgument(invocation, parameter, model);

        Assert.Equal(Lifetime.Transient, actual);
    }

    [Fact]
    public void TryResolveArgument_reads_positional_argument()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            static class Holder
            {
                public static void Register(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                {
                }

                public static void Call(IServiceCollection services)
                {
                    Register(services, ServiceLifetime.Scoped);
                }
            }
            """;

        var (invocation, parameter, model) = CompileRegisterCall(source, "implementationLifetime");

        var actual = LifetimeResolution.TryResolveArgument(invocation, parameter, model);

        Assert.Equal(Lifetime.Scoped, actual);
    }

    [Fact]
    public void TryResolveArgument_returns_null_when_argument_omitted()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            static class Holder
            {
                public static void Register(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                {
                }

                public static void Call(IServiceCollection services)
                {
                    Register(services);
                }
            }
            """;

        var (invocation, parameter, model) = CompileRegisterCall(source, "implementationLifetime");

        var actual = LifetimeResolution.TryResolveArgument(invocation, parameter, model);

        Assert.Null(actual);
    }

    private static (SemanticModel Model, ExpressionSyntax Expression) CompileFieldInitializer(string expression)
    {
        var source = $$"""
            using Microsoft.Extensions.DependencyInjection;

            class C
            {
                object _ = {{expression}};
            }
            """;
        return CompileAndGetFieldInitializer(source);
    }

    private static (SemanticModel Model, ExpressionSyntax Expression) CompileAndGetFieldInitializer(string source)
    {
        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var field = tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>().Last();
        var expression = field.Initializer?.Value
            ?? throw new InvalidOperationException("Expected a field initializer expression.");
        return (model, expression);
    }

    private static (InvocationExpressionSyntax Invocation, IParameterSymbol Parameter, SemanticModel Model)
        CompileRegisterCall(string source, string parameterName)
    {
        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var invocation = tree.GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "Register" });

        var method = (IMethodSymbol)model.GetSymbolInfo(invocation).Symbol!;
        var parameter = method.Parameters.Single(p => p.Name == parameterName);
        return (invocation, parameter, model);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
        var references = ImmutableArray.Create<MetadataReference>(
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceLifetime).Assembly.Location));

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            var extras = trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Where(path =>
                    path.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => MetadataReference.CreateFromFile(path));
            references = references.AddRange(extras);
        }

        return CSharpCompilation.Create(
            "DiLifetimeResolutionTests",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
