using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class DecoratorStackSyntaxFactory
{
    public static string GetStackClassName(INamedTypeSymbol serviceType) =>
        GetStackClassName(serviceType.Name);

    public static string GetOrderClassName(INamedTypeSymbol serviceType) =>
        GetOrderClassName(serviceType.Name);

    public static string GetStackClassName(string serviceTypeName) =>
        GetBaseName(serviceTypeName) + "DecoratorStack";

    public static string GetOrderClassName(string serviceTypeName) =>
        GetBaseName(serviceTypeName) + "DecoratorOrder";

    public static CompilationUnitSyntax CreateOrderCompilationUnit(
        string? namespaceName,
        string orderClassName,
        IReadOnlyList<(string ConstantName, int OrderValue)> orders)
    {
        var members = orders.Select(order =>
            SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)))
                        .AddVariables(
                            SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(order.ConstantName))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.NumericLiteralExpression,
                                            SyntaxFactory.Literal(order.OrderValue))))))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.ConstKeyword))));

        var orderClass = SyntaxFactory.ClassDeclaration(orderClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        return WrapInCompilationUnit(namespaceName, orderClass);
    }

    public static CompilationUnitSyntax CreateStackCompilationUnit(
        string? namespaceName,
        string stackClassName,
        string serviceTypeName,
        IReadOnlyList<(string TypeName, bool IsAsync, bool IsSync)> decoratorEntries,
        bool hasAsyncDecorators,
        GeneratorIntegrationOptions integrationOptions)
    {
        var serviceTypeSyntax = SyntaxFactory.ParseTypeName(serviceTypeName);

        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("DecoratorStackBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(serviceTypeSyntax)));

        // Build(core) — sync, always emitted. Only adds sync decorators.
        ExpressionSyntax syncBuilderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var entry in decoratorEntries)
        {
            if (entry.IsSync)
            {
                syncBuilderExpression = AddSyncDecorator(syncBuilderExpression, entry.TypeName);
            }
        }

        var syncBuildInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    syncBuilderExpression,
                    SyntaxFactory.IdentifierName("Build")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("core")))));

        var syncBuildMethod = SyntaxFactory.MethodDeclaration(
                serviceTypeSyntax,
                SyntaxFactory.Identifier("Build"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("core"))
                            .WithType(serviceTypeSyntax))))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.ReturnStatement(syncBuildInvocation))));

        var members = new List<MemberDeclarationSyntax> { syncBuildMethod };

        // BuildAsync(core, ct) — emitted when any decorator is async.
        // Adds all decorators: sync via Add<T>(), async via Add((IAsyncDecorator<T>)new T()).
        if (hasAsyncDecorators)
        {
            members.Add(CreateBuildAsyncMethod(serviceTypeSyntax, serviceTypeName, decoratorEntries));
        }

        // DI integration methods
        if (integrationOptions.EnableDi)
        {
            members.Add(CreateBuildFromServiceProviderMethod(serviceTypeSyntax, serviceTypeName, decoratorEntries, hasAsyncDecorators));
            members.Add(CreateRegisterDiMethod(serviceTypeSyntax, decoratorEntries));
        }

        var stackClass = SyntaxFactory.ClassDeclaration(stackClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        var additionalUsings = new List<string> { "DesignPatterns.Structural" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.Add("Microsoft.Extensions.DependencyInjection");
            additionalUsings.Add("Microsoft.Extensions.DependencyInjection.Extensions");
        }

        return WrapInCompilationUnit(namespaceName, stackClass, additionalUsings.ToArray());
    }

    private static ExpressionSyntax AddSyncDecorator(ExpressionSyntax builderExpression, string decoratorTypeName)
    {
        var addMethod = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Add"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(decoratorTypeName))));

        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    addMethod))
            .WithArgumentList(SyntaxFactory.ArgumentList());
    }

    private static ExpressionSyntax AddAsyncDecorator(ExpressionSyntax builderExpression, string decoratorTypeName, string serviceTypeName)
    {
        // Add((IAsyncDecorator<TService>)new TDecorator())
        var newExpression = SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName(decoratorTypeName))
            .WithArgumentList(SyntaxFactory.ArgumentList());

        var asyncDecoratorType = SyntaxFactory.ParseTypeName($"global::DesignPatterns.Structural.IAsyncDecorator<{serviceTypeName}>");

        var castExpression = SyntaxFactory.CastExpression(
            asyncDecoratorType,
            newExpression);

        return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("Add")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(castExpression))));
    }

    private static MethodDeclarationSyntax CreateBuildAsyncMethod(
        TypeSyntax serviceTypeSyntax,
        string serviceTypeName,
        IReadOnlyList<(string TypeName, bool IsAsync, bool IsSync)> decoratorEntries)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("DecoratorStackBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(serviceTypeSyntax)));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var entry in decoratorEntries)
        {
            if (entry.IsSync)
            {
                builderExpression = AddSyncDecorator(builderExpression, entry.TypeName);
            }
            else if (entry.IsAsync)
            {
                builderExpression = AddAsyncDecorator(builderExpression, entry.TypeName, serviceTypeName);
            }
        }

        var buildAsyncInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("BuildAsync")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("core")),
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("cancellationToken")),
                        })));

        var valueTaskType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("ValueTask"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(serviceTypeSyntax)));

        return SyntaxFactory.MethodDeclaration(
                valueTaskType,
                SyntaxFactory.Identifier("BuildAsync"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                        new ParameterSyntax[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("core"))
                                .WithType(serviceTypeSyntax),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                                .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"))
                                .WithDefault(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.DefaultExpression(
                                            SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken")))),
                        })))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.ReturnStatement(buildAsyncInvocation))));
    }

    private static MethodDeclarationSyntax CreateBuildFromServiceProviderMethod(
        TypeSyntax serviceTypeSyntax,
        string serviceTypeName,
        IReadOnlyList<(string TypeName, bool IsAsync, bool IsSync)> decoratorEntries,
        bool hasAsyncDecorators)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("DecoratorStackBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(serviceTypeSyntax)));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var entry in decoratorEntries)
        {
            var resolveExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("serviceProvider"),
                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("GetRequiredService"))
                            .WithTypeArgumentList(
                                SyntaxFactory.TypeArgumentList(
                                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                        SyntaxFactory.ParseTypeName(entry.TypeName))))))
                .WithArgumentList(SyntaxFactory.ArgumentList());

            // For sync decorators, Add(resolvedInstance) works because the resolved type implements IDecorator<T>.
            // For async-only decorators, we need to cast to IAsyncDecorator<T>.
            ArgumentSyntax addArg;
            if (entry.IsSync)
            {
                addArg = SyntaxFactory.Argument(resolveExpression);
            }
            else
            {
                var asyncDecoratorType = SyntaxFactory.ParseTypeName($"global::DesignPatterns.Structural.IAsyncDecorator<{serviceTypeName}>");
                addArg = SyntaxFactory.Argument(SyntaxFactory.CastExpression(asyncDecoratorType, resolveExpression));
            }

            builderExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        builderExpression,
                        SyntaxFactory.IdentifierName("Add")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(addArg)));
        }

        var buildMethodName = hasAsyncDecorators ? "BuildAsync" : "Build";
        var buildInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName(buildMethodName)))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        hasAsyncDecorators
                            ? new ArgumentSyntax[]
                            {
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("core")),
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("cancellationToken")),
                            }
                            : new ArgumentSyntax[]
                            {
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("core")),
                            })));

        var serviceProviderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var coreParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("core"))
            .WithType(serviceTypeSyntax);

        if (hasAsyncDecorators)
        {
            var ctParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"))
                .WithDefault(
                    SyntaxFactory.EqualsValueClause(
                        SyntaxFactory.DefaultExpression(
                            SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken"))));

            var valueTaskType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("ValueTask"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(serviceTypeSyntax)));

            return SyntaxFactory.MethodDeclaration(
                    valueTaskType,
                    SyntaxFactory.Identifier("BuildAsync"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.AsyncKeyword)))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList<ParameterSyntax>(
                            new ParameterSyntax[] { serviceProviderParam, coreParam, ctParam })))
                .WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ReturnStatement(buildInvocation))));
        }

        return SyntaxFactory.MethodDeclaration(
                serviceTypeSyntax,
                SyntaxFactory.Identifier("Build"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                        new ParameterSyntax[] { serviceProviderParam, coreParam })))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.ReturnStatement(buildInvocation))));
    }

    private static MethodDeclarationSyntax CreateRegisterDiMethod(
        TypeSyntax serviceTypeSyntax,
        IReadOnlyList<(string TypeName, bool IsAsync, bool IsSync)> decoratorEntries)
    {
        var statements = new List<StatementSyntax>();

        foreach (var entry in decoratorEntries)
        {
            statements.Add(SyntaxFactory.ParseStatement(
                $"services.TryAdd(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof({entry.TypeName}), {entry.TypeName}, implementationLifetime));"));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("services")));

        var servicesParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("services"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"));

        var lifetimeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("implementationLifetime"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"),
                        SyntaxFactory.IdentifierName("Singleton"))));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                SyntaxFactory.Identifier("RegisterDi"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                        new ParameterSyntax[] { servicesParam, lifetimeParam })))
            .WithBody(SyntaxFactory.Block(statements));
    }

    public static string GetBaseName(string name)
    {
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        return name;
    }

    private static CompilationUnitSyntax WrapInCompilationUnit(
        string? namespaceName,
        TypeDeclarationSyntax typeDeclaration,
        params string[] additionalUsings)
    {
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithLeadingTrivia(CreateAutoGeneratedHeader())
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")));

        foreach (var additionalUsing in additionalUsings)
        {
            compilationUnit = compilationUnit.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(additionalUsing)));
        }

        MemberDeclarationSyntax member = typeDeclaration;
        if (!string.IsNullOrEmpty(namespaceName))
        {
            member = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName!))
                .AddMembers(typeDeclaration);
        }

        return compilationUnit.AddMembers(member).NormalizeWhitespace();
    }

    private static SyntaxTriviaList CreateAutoGeneratedHeader() =>
        SyntaxFactory.TriviaList(
            SyntaxFactory.Comment("// <auto-generated />"),
            SyntaxFactory.EndOfLine("\n"),
            SyntaxFactory.Comment("// Generated by DesignPatterns.SourceGenerators.DecoratorGenerator"),
            SyntaxFactory.EndOfLine("\n"));
}
