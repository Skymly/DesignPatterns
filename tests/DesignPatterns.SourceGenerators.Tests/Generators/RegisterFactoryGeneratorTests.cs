using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class RegisterFactoryGeneratorTests
{
    [Fact]
    public Task GeneratesKeysAndRegistry()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard")]
            public sealed class StandardProductFactory : IProductFactory
            {
                public string Create() => "Standard";
            }

            [RegisterFactory<IProductFactory>("premium")]
            public sealed class PremiumProductFactory : IProductFactory
            {
                public string Create() => "Premium";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesKeysAndRegistryWithNonGenericAttribute()
    {
        const string source = """
            using System;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public sealed class StandardProductFactory : IProductFactory
            {
                public string Create() => "Standard";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesSourcesForSameNamedContractsInDifferentNamespaces()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace Catalog
            {
                public interface IProductFactory
                {
                    string Create();
                }

                [RegisterFactory<IProductFactory>("standard")]
                public sealed class StandardProductFactory : IProductFactory
                {
                    public string Create() => "standard";
                }
            }

            namespace Fulfillment
            {
                public interface IProductFactory
                {
                    string Create();
                }

                [RegisterFactory<IProductFactory>("shipment")]
                public sealed class ShipmentProductFactory : IProductFactory
                {
                    public string Create() => "shipment";
                }
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp020DuplicateKey()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard")]
            public sealed class StandardProductFactory : IProductFactory
            {
                public string Create() => "Standard";
            }

            [RegisterFactory<IProductFactory>("standard")]
            public sealed class DuplicateFactory : IProductFactory
            {
                public string Create() => "Duplicate";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp021ContractMismatch()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("broken")]
            public sealed class BrokenFactory
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp022MissingParameterlessConstructor()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("custom")]
            public sealed class CustomFactory : IProductFactory
            {
                public CustomFactory(string endpoint) => Endpoint = endpoint;

                public string Endpoint { get; }

                public string Create() => Endpoint;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
