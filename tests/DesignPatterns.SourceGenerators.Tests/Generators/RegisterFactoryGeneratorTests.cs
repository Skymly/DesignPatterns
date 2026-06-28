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

    [Fact]
    public Task GeneratesAsyncRegistryWhenFactoryImplementsIAsyncFactory()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard")]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }

            [RegisterFactory<IProductFactory>("premium")]
            public sealed class PremiumProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Premium";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new PremiumProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesPooledRegistryWhenPoolSizeIsSet()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard", PoolSize = 8)]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesMixedSyncAndAsyncRegistries()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
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

            [RegisterFactory<IProductFactory>("premium", IsAsync = true)]
            public sealed class PremiumProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Premium";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new PremiumProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesAsyncRegistryWithNonGenericAttribute()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory("standard", typeof(IProductFactory))]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp053AsyncSignatureMismatch()
    {
        const string source = """
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("broken", IsAsync = true)]
            public sealed class BrokenFactory : IProductFactory
            {
                public string Create() => "Broken";
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp054PoolSizeInvalid()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("broken", PoolSize = -1)]
            public sealed class BrokenFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Broken";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new BrokenFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp055PoolSizeTooLarge()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("big", PoolSize = 2048)]
            public sealed class BigPoolFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Big";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new BigPoolFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task EmitsRegisterDiWhenDiIntegrationEnabledForAsyncFactory()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard")]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            enableDiIntegration: true,
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task EmitsRegisterDiWhenDiIntegrationEnabledForPooledFactory()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard", PoolSize = 8)]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            enableDiIntegration: true,
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task EmitsRegisterAutofacWhenAutofacIntegrationEnabledForAsyncFactory()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard")]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            enableDiIntegration: false,
            enableAutofacIntegration: true,
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task EmitsRegisterAutofacWhenAutofacIntegrationEnabledForPooledFactory()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using DesignPatterns.Creational;

            namespace TestAssembly;

            public interface IProductFactory
            {
                string Create();
            }

            [RegisterFactory<IProductFactory>("standard", PoolSize = 8)]
            public sealed class StandardProductFactory : IProductFactory, IAsyncFactory<IProductFactory>
            {
                public string Create() => "Standard";

                public ValueTask<IProductFactory> CreateAsync(CancellationToken cancellationToken = default) =>
                    new ValueTask<IProductFactory>(new StandardProductFactory());
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<RegisterFactoryGenerator>(
            enableDiIntegration: false,
            enableAutofacIntegration: true,
            ("Factories.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }
}
