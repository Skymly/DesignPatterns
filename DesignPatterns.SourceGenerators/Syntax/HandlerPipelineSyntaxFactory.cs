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
        GetPipelineClassName(contextType.Name);

    public static string GetPipelineClassName(string contextTypeName) =>
        contextTypeName + "HandlerPipeline";

    public static CompilationUnitSyntax CreatePipelineCompilationUnit(
        string? namespaceName,
        string pipelineClassName,
        string contextTypeName,
        IReadOnlyList<(string HandlerTypeName, string? GuardMethodReference)> handlers,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        var buildInvocation = DiIntegrationSyntaxHelper.CreateHandlerPipelineBuilderExpression(
            contextTypeName,
            handlers,
            RegistrationResolveTarget.DirectNew);

        var handlerTypeNames = handlers
            .Select(static h => h.HandlerTypeName)
            .ToList();

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

        var instanceProperty = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.PropertyDeclaration(
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
                                SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("_instance")))))),
            "Gets the singleton registry instance.");

        var members = new List<MemberDeclarationSyntax> { instanceField, instanceProperty };

        if (integrationOptions.EnableDi)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateHandlerCreateFromServiceProviderMethod(contextTypeName, handlers));
            var pipelineType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.ParseTypeName(contextTypeName))));
            members.Add(DiIntegrationSyntaxHelper.CreateRegisterDiMethod(handlerTypeNames, pipelineType));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(AutofacIntegrationSyntaxHelper.CreateHandlerCreateFromComponentContextMethod(contextTypeName, handlers));
            var pipelineType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("HandlerPipeline"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                            SyntaxFactory.ParseTypeName(contextTypeName))));
            members.Add(AutofacIntegrationSyntaxHelper.CreateRegisterAutofacMethod(handlerTypeNames, pipelineType));
        }

        var pipelineClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(pipelineClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            $"Provides a handler pipeline for {contextTypeName}.");

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

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, pipelineClass, "HandlerOrderGenerator", additionalUsings.ToArray());
    }
}
