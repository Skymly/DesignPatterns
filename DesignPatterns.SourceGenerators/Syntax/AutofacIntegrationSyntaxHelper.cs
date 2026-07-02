using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class AutofacIntegrationSyntaxHelper
{
    internal static MethodDeclarationSyntax CreateStrategyCreateFromComponentContextMethod(string contractTypeName)
    {
        var lifetimeScopeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetimeScope"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"));

        var returnType = CreateStrategyRegistryInterfaceType(contractTypeName);

        var registryType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("ComponentContextStrategyRegistry"))
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
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("lifetimeScope")),
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_diEntries")),
                            }))));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(lifetimeScopeParam)
                .WithBody(SyntaxFactory.Block(body)),
            "Creates a registry from the Autofac component context.");
    }

    internal static MethodDeclarationSyntax CreateFactoryCreateFromComponentContextMethod(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries)
    {
        var lifetimeScopeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetimeScope"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"));

        var returnType = CreateFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = DiIntegrationSyntaxHelper.CreateFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            RegistrationResolveTarget.ComponentContext);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(lifetimeScopeParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates a registry from the Autofac component context.");
    }

    internal static MethodDeclarationSyntax CreateAsyncFactoryCreateFromComponentContextMethod(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries)
    {
        var lifetimeScopeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetimeScope"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"));

        var returnType = DiIntegrationSyntaxHelper.CreateAsyncFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = DiIntegrationSyntaxHelper.CreateAsyncFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            RegistrationResolveTarget.ComponentContext);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(lifetimeScopeParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates an async registry from the Autofac component context.");
    }

    internal static MethodDeclarationSyntax CreatePooledFactoryCreateFromComponentContextMethod(
        string contractTypeName,
        int poolSize,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries)
    {
        var lifetimeScopeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetimeScope"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"));

        var returnType = DiIntegrationSyntaxHelper.CreatePooledFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = DiIntegrationSyntaxHelper.CreatePooledFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            poolSize,
            RegistrationResolveTarget.ComponentContext);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(lifetimeScopeParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates a pooled registry from the Autofac component context.");
    }

    internal static MethodDeclarationSyntax CreateHandlerCreateFromComponentContextMethod(
        string contextTypeName,
        IReadOnlyList<(string HandlerTypeName, string? GuardMethodReference)> handlers)
    {
        var lifetimeScopeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetimeScope"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"));

        var returnType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contextTypeName))));

        var buildCall = DiIntegrationSyntaxHelper.CreateHandlerPipelineBuilderExpression(
            contextTypeName,
            handlers,
            RegistrationResolveTarget.ComponentContext);

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(lifetimeScopeParam)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            "Creates a handler pipeline from the Autofac component context.");
    }

    internal static MethodDeclarationSyntax CreateRegisterAutofacMethod(
        IReadOnlyList<string> implementationTypeNames,
        TypeSyntax registeredServiceType,
        string createMethodName = "Create")
    {
        var statements = new List<StatementSyntax>();

        foreach (var implementationTypeName in implementationTypeNames)
        {
            statements.Add(CreateRegisterImplementationStatement(implementationTypeName));
        }

        statements.Add(CreateRegisterServiceStatement(registeredServiceType, createMethodName));

        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ContainerBuilder"));

        var sharingParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("sharing"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"),
                        SyntaxFactory.IdentifierName("Shared"))));

        var serviceKeyParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceKey"))
            .WithType(SyntaxFactory.NullableType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword))))
            .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("RegisterAutofac"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam, sharingParam, serviceKeyParam)
                .WithBody(SyntaxFactory.Block(statements)),
            "Registers the registry and all implementations with Autofac.");
    }

    internal static string[] GetAutofacUsings() =>
        new[]
        {
            "Autofac",
            "DesignPatterns.Extensions.Autofac",
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

    private static StatementSyntax CreateRegisterImplementationStatement(string implementationTypeName)
    {
        var registrationIdentifier = SyntaxFactory.Identifier("registration");

        var registerInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("builder"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("RegisterType"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(implementationTypeName))))))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        var sharedCondition = SyntaxFactory.BinaryExpression(
            SyntaxKind.EqualsExpression,
            SyntaxFactory.IdentifierName("sharing"),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"),
                SyntaxFactory.IdentifierName("Shared")));

        var sharedLifetime = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(registrationIdentifier),
                    SyntaxFactory.IdentifierName("SingleInstance"))));

        var transientLifetime = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(registrationIdentifier),
                    SyntaxFactory.IdentifierName("InstancePerDependency"))));

        return SyntaxFactory.Block(
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(registrationIdentifier)
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(registerInvocation)))),
            SyntaxFactory.IfStatement(
                sharedCondition,
                SyntaxFactory.Block(sharedLifetime),
                SyntaxFactory.ElseClause(SyntaxFactory.Block(transientLifetime))));
    }

    private static StatementSyntax CreateRegisterServiceStatement(
        TypeSyntax registeredServiceType,
        string createMethodName)
    {
        var contextParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("ctx"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.IComponentContext"));

        var createCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(createMethodName))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("ctx"),
                                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("Resolve"))
                                            .WithTypeArgumentList(
                                                SyntaxFactory.TypeArgumentList(
                                                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                        SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"))))))
                                .WithArgumentList(SyntaxFactory.ArgumentList())))));

        var lambda = SyntaxFactory.ParenthesizedLambdaExpression()
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(contextParam)))
            .WithExpressionBody(createCall);

        var registerTyped = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("builder"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Register"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(registeredServiceType)))),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(lambda))));

        var defaultRegistration = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        registerTyped,
                        SyntaxFactory.IdentifierName("SingleInstance")))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

        var registerUntyped = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("builder"),
                    SyntaxFactory.IdentifierName("Register")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(lambda))));

        var keyedRegistration = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    registerUntyped,
                                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Keyed"))
                                        .WithTypeArgumentList(
                                            SyntaxFactory.TypeArgumentList(
                                                SyntaxFactory.SingletonSeparatedList(registeredServiceType)))))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("serviceKey"))))),
                        SyntaxFactory.IdentifierName("SingleInstance")))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

        var keyedCondition = SyntaxFactory.BinaryExpression(
            SyntaxKind.NotEqualsExpression,
            SyntaxFactory.IdentifierName("serviceKey"),
            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

        return SyntaxFactory.IfStatement(
            keyedCondition,
            SyntaxFactory.Block(keyedRegistration),
            SyntaxFactory.ElseClause(SyntaxFactory.Block(defaultRegistration)));
    }
}
