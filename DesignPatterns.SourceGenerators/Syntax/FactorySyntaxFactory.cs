using System;
using System.Collections.Generic;
using System.Linq;
using DesignPatterns.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class FactorySyntaxFactory
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

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, keysClass, "RegisterFactoryGenerator");
    }

    public static CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName)> entries,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        var returnType = CreateFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = DiIntegrationSyntaxHelper.CreateFactoryRegistryBuilderExpression(
            contractTypeName,
            entries,
            RegistrationResolveTarget.DirectNew);

        var members = new List<MemberDeclarationSyntax>
        {
            SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
        };

        if (integrationOptions.EnableDi)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateFactoryCreateFromServiceProviderMethod(contractTypeName, entries));
            members.Add(DiIntegrationSyntaxHelper.CreateRegisterDiMethod(
                entries.Select(e => e.ImplementationTypeName).ToList(),
                returnType));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(AutofacIntegrationSyntaxHelper.CreateFactoryCreateFromComponentContextMethod(contractTypeName, entries));
            members.Add(AutofacIntegrationSyntaxHelper.CreateRegisterAutofacMethod(
                entries.Select(e => e.ImplementationTypeName).ToList(),
                returnType));
        }

        var registryClass = SyntaxFactory.ClassDeclaration(registryClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members.ToArray());

        var additionalUsings = new List<string> { "System", "DesignPatterns.Creational" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.AddRange(DiIntegrationSyntaxHelper.GetDiUsings());
        }

        if (integrationOptions.EnableAutofac)
        {
            additionalUsings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, registryClass, "RegisterFactoryGenerator", additionalUsings.ToArray());
    }

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

    public static string GetKeysClassName(INamedTypeSymbol contract) =>
        GetKeysClassName(contract.Name);

    public static string GetRegistryClassName(INamedTypeSymbol contract) =>
        GetRegistryClassName(contract.Name);

    public static string GetKeysClassName(string contractName) =>
        GetBaseName(contractName) + "Keys";

    public static string GetRegistryClassName(string contractName) =>
        GetBaseName(contractName) + "Registry";

    public static string ToConstantName(string key)
    {
        var parts = key.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "Key";
        }

        return string.Concat(parts.Select(ToPascalCaseSegment));
    }

    private static string GetBaseName(string name)
    {
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

}
