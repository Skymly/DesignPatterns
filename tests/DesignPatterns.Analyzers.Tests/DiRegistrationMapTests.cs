using System.Collections.Generic;
using System.Linq;
using DesignPatterns.Analyzers.Di;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace DesignPatterns.Analyzers.Tests;

public sealed class DiRegistrationMapTests
{
    [Fact]
    public void Build_maps_msdi_add_methods_by_lifetime()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            class SingletonService { }
            class ScopedService { }
            class TransientService { }

            static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<SingletonService>();
                    services.AddScoped<ScopedService>();
                    services.AddTransient<TransientService>();
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Singleton, GetLifetime(map, "SingletonService"));
        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "ScopedService"));
        Assert.Equal(Lifetime.Transient, GetLifetime(map, "TransientService"));
    }

    [Fact]
    public void Build_maps_msdi_two_type_args_to_implementation_type()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            interface IService { }
            class ServiceImpl : IService { }

            static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<IService, ServiceImpl>();
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Singleton, GetLifetime(map, "ServiceImpl"));
        Assert.DoesNotContain(map.Lifetimes.Keys, t => t.Name == "IService");
    }

    [Fact]
    public void Build_maps_tryadd_service_descriptor()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            class MyService { }

            static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.TryAdd(new ServiceDescriptor(typeof(MyService), typeof(MyService), ServiceLifetime.Scoped));
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "MyService"));
    }

    [Fact]
    public void Build_maps_factory_registration_with_skip_constructor_analysis()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            class ScopedService { }

            static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<ScopedService>(_ => new ScopedService());
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "ScopedService"));
        var entry = Assert.Single(map.Entries);
        Assert.True(entry.SkipConstructorAnalysis);
    }

    [Fact]
    public void Build_maps_autofac_register_type_fluent_lifetimes()
    {
        const string source = """
            using Autofac;

            class SingletonService { }
            class ScopedService { }
            class TransientService { }

            static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.RegisterType<SingletonService>().SingleInstance();
                    builder.RegisterType<ScopedService>().InstancePerLifetimeScope();
                    builder.RegisterType<TransientService>();
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Singleton, GetLifetime(map, "SingletonService"));
        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "ScopedService"));
        Assert.Equal(Lifetime.Transient, GetLifetime(map, "TransientService"));
    }

    [Fact]
    public void Build_maps_autofac_register_type_typeof_argument()
    {
        const string source = """
            using System;
            using Autofac;

            class ScopedService { }

            static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.RegisterType(typeof(ScopedService)).InstancePerLifetimeScope();
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "ScopedService"));
    }

    [Fact]
    public void Build_maps_autofac_register_delegate_with_skip_constructor_analysis()
    {
        const string source = """
            using Autofac;

            class ScopedService { }

            static class Startup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register(c => new ScopedService()).InstancePerLifetimeScope();
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "ScopedService"));
        var entry = Assert.Single(map.Entries);
        Assert.True(entry.SkipConstructorAnalysis);
    }

    [Fact]
    public void Build_last_registration_wins_for_same_type()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            class MyService { }

            static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddSingleton<MyService>();
                    services.AddTransient<MyService>();
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Transient, GetLifetime(map, "MyService"));
        Assert.Equal(2, map.Entries.Count);
    }

    [Fact]
    public void Build_ignores_register_di_calls()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;

            class MyService { }

            static class StrategyRegistryHolder
            {
                public static IServiceCollection RegisterDi(
                    IServiceCollection services,
                    ServiceLifetime implementationLifetime = ServiceLifetime.Singleton,
                    ServiceLifetime registryLifetime = ServiceLifetime.Singleton)
                    => services;
            }

            static class Startup
            {
                public static void Configure(IServiceCollection services)
                {
                    services.AddScoped<MyService>();
                    StrategyRegistryHolder.RegisterDi(services);
                }
            }
            """;

        var map = DiRegistrationMap.Build(CreateCompilation(source));

        Assert.Equal(Lifetime.Scoped, GetLifetime(map, "MyService"));
        Assert.Single(map.Entries);
    }

    private static Lifetime GetLifetime(DiRegistrationMap map, string typeName)
    {
        var type = map.Lifetimes.Keys.Single(t => t.Name == typeName);
        return map.Lifetimes[type];
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs");
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceLifetime).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Autofac.ContainerBuilder).Assembly.Location),
        };

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            foreach (var assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (assemblyPath.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase) ||
                    assemblyPath.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                    assemblyPath.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase) ||
                    assemblyPath.EndsWith($"{Path.DirectorySeparatorChar}System.ComponentModel.dll", StringComparison.OrdinalIgnoreCase))
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
            }
        }

        return CSharpCompilation.Create(
            "DiRegistrationMapTests",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
