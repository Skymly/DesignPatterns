using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal enum RegistrationResolveTarget
{
    DirectNew,
    ServiceProvider,
    ComponentContext,
}

internal static class DiIntegrationSyntaxHelper
{
    internal static MemberDeclarationSyntax CreateDiEntriesField(
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries)
    {
        var arrayType = SyntaxFactory.ParseTypeName("(string Key, global::System.Type ImplementationType)[]");

        var initializerElements = entries
            .Select(e => SyntaxFactory.ParseExpression(
                $"(\"{e.Key.Replace("\\", "\\\\").Replace("\"", "\\\"")}\", typeof({e.ImplementationTypeName}))"))
            .ToArray();

        var arrayCreation = SyntaxFactory.ArrayCreationExpression(
                SyntaxFactory.ArrayType(
                    SyntaxFactory.ParseTypeName("(string Key, global::System.Type ImplementationType)"),
                    SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier())))
            .WithInitializer(
                SyntaxFactory.InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SyntaxFactory.SeparatedList(initializerElements)));

        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(arrayType)
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("_diEntries"))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(arrayCreation))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
    }

    internal static MethodDeclarationSyntax CreateStrategyCreateFromServiceProviderMethod(
        string contractTypeName)
    {
        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var returnType = CreateStrategyRegistryInterfaceType(contractTypeName);

        var registryType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("ServiceProviderStrategyRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        var body = SyntaxFactory.ReturnStatement(
            SyntaxFactory.ObjectCreationExpression(registryType)
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList<ArgumentSyntax>(
                            new ArgumentSyntax[]
                            {
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("serviceProvider")),
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_diEntries")),
                            }))));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(serviceProviderParam)
                .WithBody(SyntaxFactory.Block(body)),
            "Creates a registry from the service provider.");
    }

    internal static MethodDeclarationSyntax CreateRegisterDiMethod(
        IReadOnlyList<string> implementationTypeNames,
        TypeSyntax registeredServiceType,
        string createMethodName = "Create") =>
        CreateRegisterDiMethodCore(
            implementationTypeNames,
            registeredServiceType,
            createMethodName,
            defaultImplementationLifetime: "Singleton",
            xmlDoc: "Registers the registry and all implementations in the DI container.");

    /// <summary>
    /// Factory-specific RegisterDi: defaults <c>implementationLifetime</c> to
    /// <c>Transient</c> because factory implementations should produce
    /// a new product per <c>Create</c>/<c>CreateAsync</c> call.
    /// </summary>
    internal static MethodDeclarationSyntax CreateRegisterDiMethodForFactory(
        IReadOnlyList<string> implementationTypeNames,
        TypeSyntax registeredServiceType,
        string createMethodName = "Create") =>
        CreateRegisterDiMethodCore(
            implementationTypeNames,
            registeredServiceType,
            createMethodName,
            defaultImplementationLifetime: "Transient",
            xmlDoc: "Registers the factory registry and all implementations in the DI container. " +
                    "Implementations default to Transient so each Create/CreateAsync call resolves a new instance.");

    private static MethodDeclarationSyntax CreateRegisterDiMethodCore(
        IReadOnlyList<string> implementationTypeNames,
        TypeSyntax registeredServiceType,
        string createMethodName,
        string defaultImplementationLifetime,
        string xmlDoc)
    {
        var statements = new List<StatementSyntax>();

        foreach (var implementationTypeName in implementationTypeNames)
        {
            statements.Add(CreateTryAddImplementationStatement(implementationTypeName));
        }

        statements.Add(CreateTryAddRegistryStatement(registeredServiceType, createMethodName));
        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("services")));

        var servicesParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("services"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"));

        var implementationLifetimeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("implementationLifetime"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"),
                        SyntaxFactory.IdentifierName(defaultImplementationLifetime))));

        var registryLifetimeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("registryLifetime"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"),
                        SyntaxFactory.IdentifierName("Singleton"))));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                    SyntaxFactory.Identifier("RegisterDi"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(servicesParam, implementationLifetimeParam, registryLifetimeParam)
                .WithBody(SyntaxFactory.Block(statements)),
            xmlDoc);
    }

    internal static MethodDeclarationSyntax CreateFactoryCreateFromServiceProviderMethod(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries)
    {
        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var returnType = CreateFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = CreateFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            RegistrationResolveTarget.ServiceProvider);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(serviceProviderParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates a registry from the service provider.");
    }

    internal static MethodDeclarationSyntax CreateHandlerCreateFromServiceProviderMethod(
        string contextTypeName,
        IReadOnlyList<(string HandlerTypeName, string? GuardMethodReference)> handlers)
    {
        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var returnType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contextTypeName))));

        var buildCall = CreateHandlerPipelineBuilderExpression(
            contextTypeName,
            handlers,
            RegistrationResolveTarget.ServiceProvider);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(serviceProviderParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates a handler pipeline from the service provider.");
    }

    internal static ExpressionSyntax CreateFactoryRegistryBuilderExpression(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries,
        RegistrationResolveTarget resolveTarget)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("FactoryRegistryBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var entry in entries)
        {
            ExpressionSyntax lambdaBody = resolveTarget switch
            {
                RegistrationResolveTarget.ServiceProvider =>
                    CreateResolveCastExpression(entry.ImplementationTypeName, contractTypeName, resolveTarget),
                RegistrationResolveTarget.ComponentContext =>
                    CreateResolveCastExpression(entry.ImplementationTypeName, contractTypeName, resolveTarget),
                _ => SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList()),
            };

            var lambda = SyntaxFactory.ParenthesizedLambdaExpression()
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithExpressionBody(lambdaBody);

            builderExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("Register")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(entry.Key))),
                            SyntaxFactory.Argument(lambda),
                        })));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));
    }

    internal static ExpressionSyntax CreateAsyncFactoryRegistryBuilderExpression(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        RegistrationResolveTarget resolveTarget)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("AsyncFactoryRegistryBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var entry in entries)
        {
            var lambda = CreateAsyncRegisterLambda(entry, contractTypeName, resolveTarget);

            builderExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("Register")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(entry.Key))),
                            SyntaxFactory.Argument(lambda),
                        })));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));
    }

    internal static ExpressionSyntax CreatePooledFactoryRegistryBuilderExpression(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        int poolSize,
        RegistrationResolveTarget resolveTarget)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("AsyncFactoryRegistryBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        // WithPooling(poolSize)
        builderExpression = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("WithPooling")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                    new ArgumentSyntax[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(poolSize))),
                    })));

        foreach (var entry in entries)
        {
            var lambda = CreateAsyncRegisterLambda(entry, contractTypeName, resolveTarget);

            builderExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("Register")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(entry.Key))),
                            SyntaxFactory.Argument(lambda),
                        })));
        }

        var buildCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));

        // Build() returns IAsyncFactoryRegistry; cast to IPooledFactoryRegistry for pooled registries.
        return SyntaxFactory.CastExpression(
            CreatePooledFactoryRegistryInterfaceType(contractTypeName),
            buildCall);
    }

    internal static MethodDeclarationSyntax CreateAsyncFactoryCreateFromServiceProviderMethod(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries)
    {
        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var returnType = CreateAsyncFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = CreateAsyncFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            RegistrationResolveTarget.ServiceProvider);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(serviceProviderParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates an async registry from the service provider.");
    }

    internal static MethodDeclarationSyntax CreatePooledFactoryCreateFromServiceProviderMethod(
        string contractTypeName,
        int poolSize,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries)
    {
        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var returnType = CreatePooledFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = CreatePooledFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            poolSize,
            RegistrationResolveTarget.ServiceProvider);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(serviceProviderParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates a pooled registry from the service provider.");
    }

    internal static ExpressionSyntax CreateHandlerPipelineBuilderExpression(
        string contextTypeName,
        IReadOnlyList<(string HandlerTypeName, string? GuardMethodReference)> handlers,
        RegistrationResolveTarget resolveTarget)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipelineBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contextTypeName))));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var (handlerTypeName, guardMethodReference) in handlers)
        {
            ExpressionSyntax handlerExpression = resolveTarget switch
            {
                RegistrationResolveTarget.ServiceProvider or RegistrationResolveTarget.ComponentContext =>
                    CreateResolveExpression(handlerTypeName, resolveTarget),
                _ => SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(handlerTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList()),
            };

            if (string.IsNullOrEmpty(guardMethodReference))
            {
                builderExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            builderExpression,
                            SyntaxFactory.IdentifierName("Use")))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(handlerExpression))));
            }
            else
            {
                var guardExpression = SyntaxFactory.ParseExpression(guardMethodReference!);
                builderExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            builderExpression,
                            SyntaxFactory.IdentifierName("Use")))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(new[]
                            {
                                SyntaxFactory.Argument(handlerExpression),
                                SyntaxFactory.Argument(guardExpression),
                            })));
            }
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));
    }

    internal static string[] GetDiUsings() =>
        new[]
        {
            "System",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Extensions",
            "DesignPatterns.Extensions.DependencyInjection",
        };

    private static GenericNameSyntax CreateStrategyRegistryInterfaceType(string contractTypeName) =>
        SyntaxFactory.GenericName(SyntaxFactory.Identifier("IStrategyRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

    private static GenericNameSyntax CreateFactoryRegistryInterfaceType(string contractTypeName) =>
        SyntaxFactory.GenericName(SyntaxFactory.Identifier("IFactoryRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

    internal static GenericNameSyntax CreateAsyncFactoryRegistryInterfaceType(string contractTypeName) =>
        SyntaxFactory.GenericName(SyntaxFactory.Identifier("IAsyncFactoryRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

    internal static GenericNameSyntax CreatePooledFactoryRegistryInterfaceType(string contractTypeName) =>
        SyntaxFactory.GenericName(SyntaxFactory.Identifier("IPooledFactoryRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

    private static ExpressionSyntax CreateResolveExpression(
        string implementationTypeName,
        RegistrationResolveTarget resolveTarget) =>
        resolveTarget switch
        {
            RegistrationResolveTarget.ComponentContext => SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("lifetimeScope"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Resolve"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(implementationTypeName))))),
                SyntaxFactory.ArgumentList()),
            _ => SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("serviceProvider"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("GetRequiredService"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(implementationTypeName))))),
                SyntaxFactory.ArgumentList()),
        };

    private static ExpressionSyntax CreateResolveCastExpression(
        string implementationTypeName,
        string contractTypeName,
        RegistrationResolveTarget resolveTarget) =>
        SyntaxFactory.CastExpression(
            SyntaxFactory.ParseTypeName(contractTypeName),
            CreateResolveExpression(implementationTypeName, resolveTarget));

    /// <summary>
    /// Creates the lambda expression for an async/pooled registry Register call.
    /// For async factories: <c>ct => source.CreateAsync(ct)</c>
    /// For sync factories: <c>() => source</c> (wrapped by builder into async).
    /// </summary>
    private static ParenthesizedLambdaExpressionSyntax CreateAsyncRegisterLambda(
        (string Key, string ImplementationTypeName, bool ImplementsAsyncFactory) entry,
        string contractTypeName,
        RegistrationResolveTarget resolveTarget)
    {
        if (entry.ImplementsAsyncFactory)
        {
            // ct => source.CreateAsync(ct)
            ExpressionSyntax sourceExpression = resolveTarget switch
            {
                RegistrationResolveTarget.ServiceProvider =>
                    CreateResolveExpression(entry.ImplementationTypeName, resolveTarget),
                RegistrationResolveTarget.ComponentContext =>
                    CreateResolveExpression(entry.ImplementationTypeName, resolveTarget),
                _ => SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList()),
            };

            var createAsyncCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    sourceExpression,
                    SyntaxFactory.IdentifierName("CreateAsync")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("ct")))));

            return SyntaxFactory.ParenthesizedLambdaExpression()
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("ct"))
                                .WithType(SyntaxFactory.IdentifierName("CancellationToken")))))
                .WithExpressionBody(createAsyncCall);
        }
        else
        {
            // () => source (sync factory wrapped by builder)
            ExpressionSyntax lambdaBody = resolveTarget switch
            {
                RegistrationResolveTarget.ServiceProvider =>
                    CreateResolveCastExpression(entry.ImplementationTypeName, contractTypeName, resolveTarget),
                RegistrationResolveTarget.ComponentContext =>
                    CreateResolveCastExpression(entry.ImplementationTypeName, contractTypeName, resolveTarget),
                _ => SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList()),
            };

            return SyntaxFactory.ParenthesizedLambdaExpression()
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithExpressionBody(lambdaBody);
        }
    }

    private static ExpressionSyntax CreateGetRequiredServiceExpression(string implementationTypeName) =>
        CreateResolveExpression(implementationTypeName, RegistrationResolveTarget.ServiceProvider);

    private static ExpressionSyntax CreateGetRequiredServiceCastExpression(
        string implementationTypeName,
        string contractTypeName) =>
        CreateResolveCastExpression(implementationTypeName, contractTypeName, RegistrationResolveTarget.ServiceProvider);

    private static StatementSyntax CreateTryAddImplementationStatement(string implementationTypeName)
    {
        var descriptorCreation = SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor"))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(implementationTypeName))),
                            SyntaxFactory.Argument(
                                SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseTypeName(implementationTypeName))),
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("implementationLifetime")),
                        })));

        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("services"),
                        SyntaxFactory.IdentifierName("TryAdd")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(descriptorCreation)))));
    }

    private static StatementSyntax CreateTryAddRegistryStatement(
        TypeSyntax registeredServiceType,
        string createMethodName)
    {
        var registryFactoryLambda = SyntaxFactory.ParenthesizedLambdaExpression()
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("sp"))
                            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider")))))
            .WithExpressionBody(
                SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName(createMethodName))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("sp"))))));

        var descriptorCreation = SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor"))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.TypeOfExpression(registeredServiceType)),
                            SyntaxFactory.Argument(registryFactoryLambda),
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("registryLifetime")),
                        })));

        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("services"),
                        SyntaxFactory.IdentifierName("TryAdd")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(descriptorCreation)))));
    }
}
