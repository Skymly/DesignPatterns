using System;
using System.Collections.Generic;
using System.Linq;
using DesignPatterns.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

/// <summary>
/// Builds the generated <c>{Event}HandlerRegistry</c> compilation unit for
/// <c>[RegisterEventHandler]</c> implementations.
/// </summary>
internal static class EventAggregatorSyntaxFactory
{
    public static CompilationUnitSyntax CreateHandlerRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string eventTypeName,
        IReadOnlyList<string> staticHandlerTypeNames,
        IReadOnlyList<string> diHandlerTypeNames,
        GeneratorIntegrationOptions integrationOptions)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Static path: SubscribeAll(IEventAggregator) instantiates each handler with new().
        // Only includes handlers with a public parameterless constructor.
        if (staticHandlerTypeNames.Count > 0)
        {
            members.Add(CreateSubscribeAllStaticMethod(eventTypeName, staticHandlerTypeNames));
        }

        if (integrationOptions.EnableDi)
        {
            // DI path: RegisterDi registers handler implementations + the registry itself.
            members.Add(CreateRegisterDiMethod(diHandlerTypeNames, eventTypeName));
            // DI path: SubscribeAll(IEventAggregator, IServiceProvider) resolves from container.
            members.Add(CreateSubscribeAllFromServiceProviderMethod(eventTypeName, diHandlerTypeNames));
        }

        if (integrationOptions.EnableAutofac)
        {
            // Autofac path: RegisterAutofac registers handler implementations.
            members.Add(CreateRegisterAutofacMethod(diHandlerTypeNames));
            // Autofac path: SubscribeAll(IEventAggregator, ILifetimeScope) resolves from lifetime scope.
            members.Add(CreateSubscribeAllFromLifetimeScopeMethod(eventTypeName, diHandlerTypeNames));
        }

        var registryClass = SyntaxFactory.ClassDeclaration(registryClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        var additionalUsings = new List<string> { "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.AddRange(DiIntegrationSyntaxHelper.GetDiUsings());
        }

        if (integrationOptions.EnableAutofac)
        {
            additionalUsings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, registryClass, "RegisterEventHandlerGenerator", additionalUsings.ToArray());
    }

    public static string GetHandlerRegistryClassName(string eventName)
    {
        var baseName = eventName;
        const string eventSuffix = "Event";
        if (baseName.EndsWith(eventSuffix, StringComparison.Ordinal) && baseName.Length > eventSuffix.Length)
        {
            baseName = baseName.Substring(0, baseName.Length - eventSuffix.Length);
        }

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = eventName;
        }

        return baseName + "EventHandlerRegistry";
    }

    private static MethodDeclarationSyntax CreateSubscribeAllStaticMethod(
        string eventTypeName,
        IReadOnlyList<string> handlerTypeNames)
    {
        var aggregatorParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("aggregator"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.IEventAggregator"));

        var statements = new List<StatementSyntax>();
        foreach (var handlerTypeName in handlerTypeNames)
        {
            var subscribeCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("aggregator"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Subscribe"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(eventTypeName))))),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(handlerTypeName))
                                .WithArgumentList(SyntaxFactory.ArgumentList())))));

            statements.Add(SyntaxFactory.ExpressionStatement(subscribeCall));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("aggregator")));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.IEventAggregator"),
                SyntaxFactory.Identifier("SubscribeAll"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(aggregatorParam)
            .WithBody(SyntaxFactory.Block(statements));
    }

    private static MethodDeclarationSyntax CreateSubscribeAllFromServiceProviderMethod(
        string eventTypeName,
        IReadOnlyList<string> handlerTypeNames)
    {
        var aggregatorParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("aggregator"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.IEventAggregator"));

        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var statements = new List<StatementSyntax>();
        foreach (var handlerTypeName in handlerTypeNames)
        {
            var resolveExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("serviceProvider"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("GetRequiredService"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(handlerTypeName))))),
                SyntaxFactory.ArgumentList());

            var subscribeCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("aggregator"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Subscribe"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(eventTypeName))))),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(resolveExpression))));

            statements.Add(SyntaxFactory.ExpressionStatement(subscribeCall));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("aggregator")));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.IEventAggregator"),
                SyntaxFactory.Identifier("SubscribeAll"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(aggregatorParam, serviceProviderParam)
            .WithBody(SyntaxFactory.Block(statements));
    }

    private static MethodDeclarationSyntax CreateRegisterDiMethod(
        IReadOnlyList<string> handlerTypeNames,
        string eventTypeName)
    {
        var statements = new List<StatementSyntax>();

        foreach (var handlerTypeName in handlerTypeNames)
        {
            statements.Add(CreateTryAddImplementationStatement(handlerTypeName));
        }

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
                        SyntaxFactory.IdentifierName("Transient"))));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                SyntaxFactory.Identifier("RegisterDi"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(servicesParam, implementationLifetimeParam)
            .WithBody(SyntaxFactory.Block(statements));
    }

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

    private static MethodDeclarationSyntax CreateRegisterAutofacMethod(
        IReadOnlyList<string> handlerTypeNames)
    {
        var statements = new List<StatementSyntax>();

        foreach (var handlerTypeName in handlerTypeNames)
        {
            var registerCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("builder"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("RegisterType"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(handlerTypeName))))),
                SyntaxFactory.ArgumentList());

            statements.Add(SyntaxFactory.ExpressionStatement(registerCall));
        }

        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ContainerBuilder"));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                SyntaxFactory.Identifier("RegisterAutofac"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(builderParam)
            .WithBody(SyntaxFactory.Block(statements));
    }

    private static MethodDeclarationSyntax CreateSubscribeAllFromLifetimeScopeMethod(
        string eventTypeName,
        IReadOnlyList<string> handlerTypeNames)
    {
        var aggregatorParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("aggregator"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.IEventAggregator"));

        var lifetimeScopeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetimeScope"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ILifetimeScope"));

        var statements = new List<StatementSyntax>();
        foreach (var handlerTypeName in handlerTypeNames)
        {
            var resolveExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("lifetimeScope"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Resolve"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(handlerTypeName))))),
                SyntaxFactory.ArgumentList());

            var subscribeCall = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("aggregator"),
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Subscribe"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                    SyntaxFactory.ParseTypeName(eventTypeName))))),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(resolveExpression))));

            statements.Add(SyntaxFactory.ExpressionStatement(subscribeCall));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("aggregator")));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.IEventAggregator"),
                SyntaxFactory.Identifier("SubscribeAll"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(aggregatorParam, lifetimeScopeParam)
            .WithBody(SyntaxFactory.Block(statements));
    }

}
