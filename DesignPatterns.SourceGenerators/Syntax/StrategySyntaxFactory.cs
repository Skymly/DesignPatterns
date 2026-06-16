using System;
using System.Collections.Generic;
using System.Linq;
using DesignPatterns.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class StrategySyntaxFactory
{
    public static CompilationUnitSyntax CreateKeysCompilationUnit(
        string? namespaceName,
        string keysClassName,
        IReadOnlyList<(string ConstantName, string KeyValue)> keys)
    {
        var members = keys.Select(k =>
            SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)))
                        .AddVariables(
                            SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(k.ConstantName))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            SyntaxFactory.Literal(k.KeyValue))))))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.ConstKeyword))));

        var keysClass = SyntaxFactory.ClassDeclaration(keysClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        return WrapInCompilationUnit(namespaceName, keysClass);
    }

    public static CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        var dictionaryType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Dictionary"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        var dictionaryInitializer = SyntaxFactory.InitializerExpression(
            SyntaxKind.ObjectInitializerExpression,
            SyntaxFactory.SeparatedList<ExpressionSyntax>(
                entries.Select(e =>
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.ImplicitElementAccess()
                            .WithArgumentList(
                                SyntaxFactory.BracketedArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal(e.Key)))))),
                        SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(e.ImplementationTypeName))
                            .WithArgumentList(SyntaxFactory.ArgumentList())))));

        var dictionaryCreation = SyntaxFactory.ObjectCreationExpression(dictionaryType)
            .WithInitializer(dictionaryInitializer);

        var strategyRegistryType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("StrategyRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        var registryInstanceCreation = SyntaxFactory.ObjectCreationExpression(strategyRegistryType)
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(dictionaryCreation))));

        var registryFieldType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("IStrategyRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        var registryField = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(registryFieldType)
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("_instance"))
                            .WithInitializer(
                                SyntaxFactory.EqualsValueClause(registryInstanceCreation))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

        var instanceProperty = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("IStrategyRegistry"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList<TypeSyntax>(
                                new TypeSyntax[]
                                {
                                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                                    SyntaxFactory.ParseTypeName(contractTypeName),
                                }))),
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

        var members = new List<MemberDeclarationSyntax> { registryField, instanceProperty };

        if (integrationOptions.NeedsRegistrationEntries)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateDiEntriesField(entries));
        }

        if (integrationOptions.EnableDi)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateStrategyCreateFromServiceProviderMethod(contractTypeName));
            members.Add(DiIntegrationSyntaxHelper.CreateRegisterDiMethod(
                entries.Select(e => e.ImplementationTypeName).ToList(),
                CreateStrategyRegistryInterfaceType(contractTypeName)));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(AutofacIntegrationSyntaxHelper.CreateStrategyCreateFromComponentContextMethod(contractTypeName));
            members.Add(AutofacIntegrationSyntaxHelper.CreateRegisterAutofacMethod(
                entries.Select(e => e.ImplementationTypeName).ToList(),
                CreateStrategyRegistryInterfaceType(contractTypeName)));
        }

        var registryClass = SyntaxFactory.ClassDeclaration(registryClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        var additionalUsings = new List<string> { "System.Collections.Generic", "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.AddRange(DiIntegrationSyntaxHelper.GetDiUsings());
        }

        if (integrationOptions.EnableAutofac)
        {
            additionalUsings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return WrapInCompilationUnit(namespaceName, registryClass, additionalUsings.ToArray());
    }

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

    public static string GetKeysClassName(INamedTypeSymbol contract) =>
        GetBaseName(contract) + "Keys";

    public static string GetRegistryClassName(INamedTypeSymbol contract) =>
        GetBaseName(contract) + "Registry";

    public static string ToConstantName(string key)
    {
        var parts = key.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "Key";
        }

        return string.Concat(parts.Select(ToPascalCaseSegment));
    }

    private static string GetBaseName(INamedTypeSymbol contract)
    {
        var name = contract.Name;
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        return name;
    }

    private static string ToPascalCaseSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return string.Empty;
        }

        if (segment.Length == 1)
        {
            return segment.ToUpperInvariant();
        }

        return char.ToUpperInvariant(segment[0]) + segment.Substring(1);
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
            SyntaxFactory.Comment("// Generated by DesignPatterns.SourceGenerators.RegisterStrategyGenerator"),
            SyntaxFactory.EndOfLine("\n"));
}
