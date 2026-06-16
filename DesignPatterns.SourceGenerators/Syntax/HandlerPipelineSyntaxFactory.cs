using System.Collections.Generic;
using System.Linq;
using DesignPatterns.SourceGenerators;
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
        IReadOnlyList<string> handlerTypeNames,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        var buildInvocation = DiIntegrationSyntaxHelper.CreateHandlerPipelineBuilderExpression(
            contextTypeName,
            handlerTypeNames,
            RegistrationResolveTarget.DirectNew);

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

        var members = new List<MemberDeclarationSyntax> { instanceField, instanceProperty };

        if (integrationOptions.EnableDi)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateHandlerCreateFromServiceProviderMethod(contextTypeName, handlerTypeNames));
            var pipelineType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.ParseTypeName(contextTypeName))));
            members.Add(DiIntegrationSyntaxHelper.CreateRegisterDiMethod(handlerTypeNames, pipelineType));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(AutofacIntegrationSyntaxHelper.CreateHandlerCreateFromComponentContextMethod(contextTypeName, handlerTypeNames));
            var pipelineType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.ParseTypeName(contextTypeName))));
            members.Add(AutofacIntegrationSyntaxHelper.CreateRegisterAutofacMethod(handlerTypeNames, pipelineType));
        }

        var pipelineClass = SyntaxFactory.ClassDeclaration(pipelineClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        var additionalUsings = new List<string> { "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi || integrationOptions.EnableAutofac)
        {
            additionalUsings.Insert(0, "System");
        }

        if (integrationOptions.EnableDi)
        {
            additionalUsings.AddRange(DiIntegrationSyntaxHelper.GetDiUsings());
        }

        if (integrationOptions.EnableAutofac)
        {
            additionalUsings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return WrapInCompilationUnit(namespaceName, pipelineClass, additionalUsings.ToArray());
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
