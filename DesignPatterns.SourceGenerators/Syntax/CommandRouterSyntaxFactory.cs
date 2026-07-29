using System;
using System.Collections.Generic;
using System.Linq;
using DesignPatterns.SourceGenerators;
using DesignPatterns.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

/// <summary>
/// Builds the generated <c>{Command}CommandHandlerRegistry</c> compilation unit for
/// <c>[RegisterCommandHandler]</c> implementations.
/// </summary>
/// <remarks>
/// This ticket owns the static (parameterless-ctor) <c>RegisterAll</c> / <c>CreateRouter</c> path.
/// <c>RegisterDi</c> and provider-based <c>RegisterAll</c> emit only when
/// <c>DesignPatterns_EnableDiIntegration</c> / Autofac flags are set (same gating as Event Aggregator).
/// Full MSDI <c>AddCommandRouter</c> extension packaging remains a DI-module follow-up.
/// </remarks>
internal static class CommandRouterSyntaxFactory
{
    public static CompilationUnitSyntax CreateHandlerRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string commandTypeName,
        string? resultTypeName,
        IReadOnlyList<string> staticHandlerTypeNames,
        IReadOnlyList<string> diHandlerTypeNames,
        IReadOnlyList<CommandPipelineBehaviorEmit> behaviors,
        GeneratorIntegrationOptions integrationOptions)
    {
        var members = new List<MemberDeclarationSyntax>();

        if (staticHandlerTypeNames.Count > 0)
        {
            members.Add(CreateRegisterAllStaticMethod(commandTypeName, resultTypeName, staticHandlerTypeNames, behaviors));
            members.Add(CreateCreateRouterMethod());
        }

        if (integrationOptions.EnableDi)
        {
            members.Add(CreateRegisterDiMethod(diHandlerTypeNames));
            members.Add(CreateRegisterAllFromServiceProviderMethod(commandTypeName, resultTypeName, diHandlerTypeNames));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(CreateRegisterAutofacMethod(diHandlerTypeNames));
            members.Add(CreateRegisterAllFromLifetimeScopeMethod(commandTypeName, resultTypeName, diHandlerTypeNames));
        }

        var registryClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(registryClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            $"Provides a command handler registry for {commandTypeName}.");

        var additionalUsings = new List<string> { "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.AddRange(DiIntegrationSyntaxHelper.GetDiUsings());
        }

        if (integrationOptions.EnableAutofac)
        {
            additionalUsings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return GeneratedCodeHelper.WrapInCompilationUnit(
            namespaceName,
            registryClass,
            "RegisterCommandHandlerGenerator",
            additionalUsings.ToArray());
    }

    public static string GetHandlerRegistryClassName(string commandName)
    {
        var baseName = commandName;
        const string commandSuffix = "Command";
        if (baseName.EndsWith(commandSuffix, StringComparison.Ordinal) && baseName.Length > commandSuffix.Length)
        {
            baseName = baseName.Substring(0, baseName.Length - commandSuffix.Length);
        }

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = commandName;
        }

        return baseName + "CommandHandlerRegistry";
    }

    private static MethodDeclarationSyntax CreateRegisterAllStaticMethod(
        string commandTypeName,
        string? resultTypeName,
        IReadOnlyList<string> handlerTypeNames,
        IReadOnlyList<CommandPipelineBehaviorEmit> behaviors)
    {
        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"));

        var statements = new List<StatementSyntax>();
        foreach (var handlerTypeName in handlerTypeNames)
        {
            statements.Add(SyntaxFactory.ExpressionStatement(
                CreateRegisterInvocation(
                    commandTypeName,
                    resultTypeName,
                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(handlerTypeName))
                        .WithArgumentList(SyntaxFactory.ArgumentList()))));
        }

        AddUseBehaviorStatements(statements, commandTypeName, resultTypeName, behaviors);

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("builder")));

        var summary = behaviors.Count > 0
            ? "Registers all parameterless command handlers and pipeline behaviors onto the builder."
            : "Registers all parameterless command handlers onto the builder.";

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"),
                    SyntaxFactory.Identifier("RegisterAll"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam)
                .WithBody(SyntaxFactory.Block(statements)),
            summary);
    }

    private static MethodDeclarationSyntax CreateCreateRouterMethod()
    {
        var body = SyntaxFactory.Block(
            SyntaxFactory.ReturnStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("RegisterAll"),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.ObjectCreationExpression(
                                                SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"))
                                            .WithArgumentList(SyntaxFactory.ArgumentList()))))),
                        SyntaxFactory.IdentifierName("Build")))));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.ICommandRouter"),
                    SyntaxFactory.Identifier("CreateRouter"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithBody(body),
            "Creates an immutable command router containing the statically registered handlers.");
    }

    private static MethodDeclarationSyntax CreateRegisterAllFromServiceProviderMethod(
        string commandTypeName,
        string? resultTypeName,
        IReadOnlyList<string> handlerTypeNames)
    {
        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"));

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

            statements.Add(SyntaxFactory.ExpressionStatement(
                CreateRegisterInvocation(commandTypeName, resultTypeName, resolveExpression)));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("builder")));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"),
                    SyntaxFactory.Identifier("RegisterAll"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam, serviceProviderParam)
                .WithBody(SyntaxFactory.Block(statements)),
            "Registers all command handlers resolved from the service provider onto the builder.");
    }

    private static MethodDeclarationSyntax CreateRegisterDiMethod(IReadOnlyList<string> handlerTypeNames)
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

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                    SyntaxFactory.Identifier("RegisterDi"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(servicesParam, implementationLifetimeParam)
                .WithBody(SyntaxFactory.Block(statements)),
            "Registers the handler implementations in the DI container.");
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

    private static MethodDeclarationSyntax CreateRegisterAutofacMethod(IReadOnlyList<string> handlerTypeNames)
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

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("RegisterAutofac"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam)
                .WithBody(SyntaxFactory.Block(statements)),
            "Registers the handler implementations with Autofac.");
    }

    private static MethodDeclarationSyntax CreateRegisterAllFromLifetimeScopeMethod(
        string commandTypeName,
        string? resultTypeName,
        IReadOnlyList<string> handlerTypeNames)
    {
        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"));

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

            statements.Add(SyntaxFactory.ExpressionStatement(
                CreateRegisterInvocation(commandTypeName, resultTypeName, resolveExpression)));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("builder")));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::DesignPatterns.Behavioral.CommandRouterBuilder"),
                    SyntaxFactory.Identifier("RegisterAll"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam, lifetimeScopeParam)
                .WithBody(SyntaxFactory.Block(statements)),
            "Registers all command handlers resolved from the Autofac lifetime scope onto the builder.");
    }

    private static void AddUseBehaviorStatements(
        List<StatementSyntax> statements,
        string commandTypeName,
        string? handlerResultTypeName,
        IReadOnlyList<CommandPipelineBehaviorEmit> behaviors)
    {
        foreach (var behavior in behaviors)
        {
            // Use the terminal handler's void/TResult contract for UseBehavior generics so a
            // mismatched behavior interface fails at C# compile time instead of Build().
            statements.Add(SyntaxFactory.ExpressionStatement(
                CreateUseBehaviorInvocation(
                    commandTypeName,
                    handlerResultTypeName,
                    behavior.BehaviorFullyQualifiedDisplayString,
                    behavior.Order)));
        }
    }

    private static InvocationExpressionSyntax CreateUseBehaviorInvocation(
        string commandTypeName,
        string? resultTypeName,
        string behaviorTypeName,
        int order)
    {
        SimpleNameSyntax useBehaviorName;
        if (resultTypeName is null)
        {
            useBehaviorName = SyntaxFactory.GenericName(SyntaxFactory.Identifier("UseBehavior"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.ParseTypeName(commandTypeName))));
        }
        else
        {
            useBehaviorName = SyntaxFactory.GenericName(SyntaxFactory.Identifier("UseBehavior"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList<TypeSyntax>(
                            new[]
                            {
                                SyntaxFactory.ParseTypeName(commandTypeName),
                                SyntaxFactory.ParseTypeName(resultTypeName),
                            })));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("builder"),
                useBehaviorName),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                    new[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(behaviorTypeName))
                                .WithArgumentList(SyntaxFactory.ArgumentList())),
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(order))),
                    })));
    }

    private static InvocationExpressionSyntax CreateRegisterInvocation(
        string commandTypeName,
        string? resultTypeName,
        ExpressionSyntax handlerExpression)
    {
        // Use explicitly typed Register overloads so DI-resolved expressions (typed as the
        // concrete handler) bind correctly even when inference would otherwise fail.
        SimpleNameSyntax registerName;
        if (resultTypeName is null)
        {
            registerName = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Register"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.ParseTypeName(commandTypeName))));
        }
        else
        {
            registerName = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Register"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList<TypeSyntax>(
                            new[]
                            {
                                SyntaxFactory.ParseTypeName(commandTypeName),
                                SyntaxFactory.ParseTypeName(resultTypeName),
                            })));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("builder"),
                registerName),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(handlerExpression))));
    }
}
