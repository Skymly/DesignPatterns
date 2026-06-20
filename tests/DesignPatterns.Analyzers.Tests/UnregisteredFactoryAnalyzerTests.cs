using System.Collections.Immutable;
using DesignPatterns.Analyzers;
using DesignPatterns.CodeFixes;
using DesignPatterns.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using VerifyTests;

namespace DesignPatterns.Analyzers.Tests;

public sealed class UnregisteredFactoryAnalyzerTests
{
    [Fact]
    public async Task ReportsDp023WhenImplementationMissingRegisterFactory()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Produce();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public class StandardFactory : IProductFactory
            {
                public string Produce() => "standard";
            }

            public class PremiumFactory : IProductFactory
            {
                public string Produce() => "premium";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredFactoryAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP023"));
    }

    [Fact]
    public async Task ReportsDp023WhenRegistrationExistsInReferencedAssembly()
    {
        const string registrationSource = """
            using DesignPatterns.Creational;

            namespace Registrations;

            public interface IProductFactory
            {
                string Produce();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public class StandardFactory : IProductFactory
            {
                public string Produce() => "standard";
            }
            """;

        const string implementationSource = """
            using DesignPatterns.Creational;
            using Registrations;

            namespace Implementations;

            public class PremiumFactory : IProductFactory
            {
                public string Produce() => "premium";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersWithReferencedAssemblyAsync(
            registrationSource,
            implementationSource,
            new UnregisteredFactoryAnalyzer());

        await Verifier.Verify(AnalyzerVerifyHelper.FormatDiagnostics(diagnostics, "DP023"));
    }
}

public sealed class AddRegisterFactoryCodeFixTests
{
    [Fact]
    public async Task FixesDp023ByAddingRegisterFactoryAttribute()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Produce();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public class StandardFactory : IProductFactory
            {
                public string Produce() => "standard";
            }

            public class PremiumFactory : IProductFactory
            {
                public string Produce() => "premium";
            }
            """;

        var diagnostics = await AnalyzerTestContext.RunAnalyzersAsync(
            source,
            new UnregisteredFactoryAnalyzer());

        var diagnostic = Assert.Single(
            diagnostics,
            d => d.Id == "DP023");

        var document = AnalyzerTestContext.CreateDocument(source);
        var fixes = ImmutableArray.CreateBuilder<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => fixes.Add(action),
            CancellationToken.None);
        await new AddRegisterFactoryCodeFixProvider().RegisterCodeFixesAsync(context);

        var operation = await fixes[0].GetOperationsAsync(CancellationToken.None);
        var applyChanges = Assert.IsType<ApplyChangesOperation>(operation.Single());
        var fixedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        await Verifier.Verify(fixedSource);
    }
}

public sealed class RegisterFactoryCodeFixTests
{
    [Fact]
    public async Task FixesDp021ByAddingContractInterface()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Produce();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public class StandardFactory
            {
                public string Produce() => "standard";
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<RegisterFactoryGenerator>(
            source,
            "DP021",
            new AddContractImplementationCodeFixProvider());

        await Verifier.Verify(fixedSource);
    }

    [Fact]
    public async Task FixesDp022ByAddingParameterlessConstructor()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Produce();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public class StandardFactory : IProductFactory
            {
                public StandardFactory(string ignored) { }

                public string Produce() => "standard";
            }
            """;

        var fixedSource = await AnalyzerTestContext.ApplyGeneratorCodeFixAsync<RegisterFactoryGenerator>(
            source,
            "DP022",
            new AddParameterlessConstructorCodeFixProvider());

        await Verifier.Verify(fixedSource);
    }
}
