using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class HandlerPipelineSyntaxFactory
{
    public static string GetPipelineClassName(INamedTypeSymbol contextType) =>
        contextType.Name + "HandlerPipeline";

    public static CompilationUnitSyntax CreatePipelineCompilationUnit(
        string? namespaceName,
        string pipelineClassName,
        string contextTypeName,
        IReadOnlyList<string> handlerTypeNames)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipelineBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contextTypeName))));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var handlerTypeName in handlerTypeNames)
        {
            builderExpression = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        builderExpression,
                        SyntaxFactory.IdentifierName("Use")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.ParseTypeName(handlerTypeName))
                                    .WithArgumentList(SyntaxFactory.ArgumentList())))));
        }

        var buildInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));

        var instanceField = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
                            .WithTypeArgumentList(
                                SyntaxFactory.TypeArgumentList(
                                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                        SyntaxFactory.ParseTypeName(contextTypeName)))))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("_instance"))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(buildInvocation))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

        var instanceProperty = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.ParseTypeName(contextTypeName)))),
                SyntaxFactory.Identifier("Instance"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(
                    SyntaxKind.GetAccessorDeclaration,
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("_instance"))))));

        var pipelineClass = SyntaxFactory.ClassDeclaration(pipelineClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(instanceField, instanceProperty);

        return WrapInCompilationUnit(namespaceName, pipelineClass, "DesignPatterns.Behavioral");
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
            SyntaxFactory.Comment("// Generated by DesignPatterns.SourceGenerators.HandlerOrderGenerator"),
            SyntaxFactory.EndOfLine("\n"));
}
