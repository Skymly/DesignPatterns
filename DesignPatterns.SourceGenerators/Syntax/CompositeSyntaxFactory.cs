using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class CompositeSyntaxFactory
{
    public static string GetKeysClassName(INamedTypeSymbol contract) =>
        GetKeysClassName(contract.Name);

    public static string GetCatalogClassName(INamedTypeSymbol contract) =>
        GetCatalogClassName(contract.Name);

    public static string GetKeysClassName(string contractName) =>
        GetBaseName(contractName) + "CompositeKeys";

    public static string GetCatalogClassName(string contractName) =>
        GetBaseName(contractName) + "CompositeCatalog";

    public static string ToConstantName(string key)
    {
        var parts = key.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "Key";
        }

        return string.Concat(parts.Select(ToPascalCaseSegment));
    }

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

    public static CompilationUnitSyntax CreateCatalogCompilationUnit(
        string? namespaceName,
        string catalogClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string? ParentKey, int Order, string ImplementationTypeName)> entries,
        GeneratorIntegrationOptions integrationOptions)
    {
        var entryType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("CompositeCatalogEntry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contractTypeName))));

        var entryInitializers = entries.Select(entry =>
        {
            ExpressionSyntax parentKeyExpression = entry.ParentKey is null
                ? SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                : SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(entry.ParentKey));

            return SyntaxFactory.ObjectCreationExpression(entryType)
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList<ArgumentSyntax>(
                            new[]
                            {
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(entry.Key))),
                                SyntaxFactory.Argument(parentKeyExpression),
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(entry.Order))),
                                SyntaxFactory.Argument(
                                    SyntaxFactory.TypeOfExpression(
                                        SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))),
                            })));
        });

        var listType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("IReadOnlyList"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(entryType)));

        var arrayCreation = SyntaxFactory.ArrayCreationExpression(
                SyntaxFactory.ArrayType(entryType)
                    .AddRankSpecifiers(SyntaxFactory.ArrayRankSpecifier()))
            .WithInitializer(
                SyntaxFactory.InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SyntaxFactory.SeparatedList<ExpressionSyntax>(
                        entryInitializers.Cast<ExpressionSyntax>())));

        var entriesField = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(listType)
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("_entries"))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(arrayCreation))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

        var assembleInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("CompositeCatalogAssembler"),
                    SyntaxFactory.IdentifierName("Assemble")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_entries")))));

        var buildRootMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(contractTypeName),
                SyntaxFactory.Identifier("BuildRoot"))
            .WithModifiers(
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.ReturnStatement(assembleInvocation))));

        var assembleForestInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("CompositeCatalogAssembler"),
                    SyntaxFactory.IdentifierName("AssembleForest")))
            .WithArgumentList(
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_entries")))));

        var forestReturnType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("IReadOnlyList"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contractTypeName))));

        var buildForestMethod = SyntaxFactory.MethodDeclaration(
                forestReturnType,
                SyntaxFactory.Identifier("BuildForest"))
            .WithModifiers(
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithBody(
                SyntaxFactory.Block(
                    SyntaxFactory.SingletonList<StatementSyntax>(
                        SyntaxFactory.ReturnStatement(assembleForestInvocation))));

        var catalogClass = SyntaxFactory.ClassDeclaration(catalogClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(entriesField, buildRootMethod, buildForestMethod);

        if (integrationOptions.EnableDi)
        {
            var buildRootFromServicesMethod = CreateBuildRootFromServicesMethod(contractTypeName);
            var buildForestFromServicesMethod = CreateBuildForestFromServicesMethod(contractTypeName);
            var registerDiMethod = CreateRegisterDiMethod(contractTypeName, entries);
            catalogClass = catalogClass.AddMembers(buildRootFromServicesMethod, buildForestFromServicesMethod, registerDiMethod);
        }

        var additionalUsings = new List<string> { "System.Collections.Generic", "DesignPatterns.Structural" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.Add("Microsoft.Extensions.DependencyInjection");
            additionalUsings.Add("Microsoft.Extensions.DependencyInjection.Extensions");
        }

        return WrapInCompilationUnit(
            namespaceName,
            catalogClass,
            additionalUsings.ToArray());
    }

    private static string GetBaseName(string name)
    {
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        return name;
    }

    private static MethodDeclarationSyntax CreateBuildRootFromServicesMethod(string contractTypeName)
    {
        var spParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var body = SyntaxFactory.Block(
            SyntaxFactory.SingletonList<StatementSyntax>(
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("CompositeCatalogAssembler"),
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("Assemble"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.ParseTypeName(contractTypeName))))))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList<ArgumentSyntax>(new[]
                                {
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_entries")),
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("serviceProvider")),
                                }))))));

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(contractTypeName),
                SyntaxFactory.Identifier("BuildRoot"))
            .WithModifiers(
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(spParam)
            .WithBody(body);
    }

    private static MethodDeclarationSyntax CreateBuildForestFromServicesMethod(string contractTypeName)
    {
        var spParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceProvider"))
            .WithType(SyntaxFactory.ParseTypeName("global::System.IServiceProvider"));

        var forestReturnType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("IReadOnlyList"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                        SyntaxFactory.ParseTypeName(contractTypeName))));

        var body = SyntaxFactory.Block(
            SyntaxFactory.SingletonList<StatementSyntax>(
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("CompositeCatalogAssembler"),
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("AssembleForest"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.ParseTypeName(contractTypeName))))))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList<ArgumentSyntax>(new[]
                                {
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("_entries")),
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("serviceProvider")),
                                }))))));

        return SyntaxFactory.MethodDeclaration(
                forestReturnType,
                SyntaxFactory.Identifier("BuildForest"))
            .WithModifiers(
                SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(spParam)
            .WithBody(body);
    }

    private static MethodDeclarationSyntax CreateRegisterDiMethod(
        string contractTypeName,
        IReadOnlyList<(string Key, string? ParentKey, int Order, string ImplementationTypeName)> entries)
    {
        var servicesParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("services"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"));

        var lifetimeParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("lifetime"))
            .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceLifetime"),
                        SyntaxFactory.IdentifierName("Singleton"))));

        var statements = new List<StatementSyntax>();

        // Register each implementation type
        foreach (var entry in entries)
        {
            statements.Add(SyntaxFactory.ParseStatement(
                $"services.TryAdd(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof({entry.ImplementationTypeName}), {entry.ImplementationTypeName}, lifetime));"));
        }

        // Register the contract type to resolve root from BuildRoot()
        statements.Add(SyntaxFactory.ParseStatement(
            $"services.TryAdd(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof({contractTypeName}), _ => BuildRoot(), global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton));"));

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("services")));

        var body = SyntaxFactory.Block(statements);

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                SyntaxFactory.Identifier("RegisterDi"))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .AddParameterListParameters(servicesParam, lifetimeParam)
            .WithBody(body);
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
            SyntaxFactory.Comment("// Generated by DesignPatterns.SourceGenerators.CompositePartGenerator"),
            SyntaxFactory.EndOfLine("\n"));
}
