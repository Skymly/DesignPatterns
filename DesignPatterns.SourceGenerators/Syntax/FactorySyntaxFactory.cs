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
            GeneratedCodeHelper.WithXmlDoc(
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
                            SyntaxFactory.Token(SyntaxKind.ConstKeyword))),
                $"The key for the {k.KeyValue} factory."));

        var keysClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(keysClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            "Provides registry keys for the factory contract.");

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, keysClass, "RegisterFactoryGenerator");
    }

    public static CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, string? GuardMethodReference)> entries,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        var returnType = CreateFactoryRegistryInterfaceType(contractTypeName);
        // Factory does not use guards; project to the 2-tuple expected by helpers.
        var entriesWithoutGuard = entries
            .Select(static e => (e.Key, e.ImplementationTypeName))
            .ToList();
        var buildCall = DiIntegrationSyntaxHelper.CreateFactoryRegistryBuilderExpression(
            contractTypeName,
            entriesWithoutGuard,
            RegistrationResolveTarget.DirectNew);

        var members = new List<MemberDeclarationSyntax>
        {
            GeneratedCodeHelper.WithXmlDoc(
                SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                "Creates a new registry instance."),
        };

        if (integrationOptions.EnableDi)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateFactoryCreateFromServiceProviderMethod(contractTypeName, entriesWithoutGuard));
            members.Add(DiIntegrationSyntaxHelper.CreateRegisterDiMethod(
                entries.Select(e => e.ImplementationTypeName).ToList(),
                returnType));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(AutofacIntegrationSyntaxHelper.CreateFactoryCreateFromComponentContextMethod(contractTypeName, entriesWithoutGuard));
            members.Add(AutofacIntegrationSyntaxHelper.CreateRegisterAutofacMethod(
                entries.Select(e => e.ImplementationTypeName).ToList(),
                returnType));
        }

        var registryClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(registryClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            $"Provides a registry for {contractTypeName}.");

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

    public static CompilationUnitSyntax? CreateAsyncRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var returnType = CreateAsyncFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = CreateAsyncFactoryRegistryBuilderExpression(contractTypeName, entries);

        var members = new List<MemberDeclarationSyntax>
        {
            GeneratedCodeHelper.WithXmlDoc(
                SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                "Creates a new async registry instance."),
        };

        var registryClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(registryClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            $"Provides an async registry for {contractTypeName}.");

        var additionalUsings = new List<string> { "System", "System.Threading", "DesignPatterns.Creational" };

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, registryClass, "RegisterFactoryGenerator", additionalUsings.ToArray());
    }

    public static CompilationUnitSyntax? CreatePooledRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        int poolSize,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        GeneratorIntegrationOptions integrationOptions = default)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        var returnType = CreatePooledFactoryRegistryInterfaceType(contractTypeName);
        var buildCall = CreatePooledFactoryRegistryBuilderExpression(contractTypeName, entries, poolSize);

        var members = new List<MemberDeclarationSyntax>
        {
            GeneratedCodeHelper.WithXmlDoc(
                SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier("Create"))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                    .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(buildCall))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                "Creates a new pooled registry instance."),
        };

        var registryClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(registryClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            $"Provides a pooled registry for {contractTypeName}.");

        var additionalUsings = new List<string> { "System", "System.Threading", "DesignPatterns.Creational" };

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, registryClass, "RegisterFactoryGenerator", additionalUsings.ToArray());
    }

    private static GenericNameSyntax CreateAsyncFactoryRegistryInterfaceType(string contractTypeName) =>
        SyntaxFactory.GenericName(SyntaxFactory.Identifier("IAsyncFactoryRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

    private static GenericNameSyntax CreatePooledFactoryRegistryInterfaceType(string contractTypeName) =>
        SyntaxFactory.GenericName(SyntaxFactory.Identifier("IPooledFactoryRegistry"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

    private static ExpressionSyntax CreateAsyncFactoryRegistryBuilderExpression(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("AsyncFactoryRegistryBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        foreach (var entry in entries)
        {
            // For async factories: ct => new Impl().CreateAsync(ct)
            // For sync factories wrapped: () => new Impl()  (uses Register(Func<T>) overload)
            ExpressionSyntax lambda;
            if (entry.ImplementsAsyncFactory)
            {
                var newExpr = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
                var createAsyncCall = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        newExpr,
                        SyntaxFactory.IdentifierName("CreateAsync")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList<ArgumentSyntax>(
                            new ArgumentSyntax[]
                            {
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("ct")),
                            })));

                lambda = SyntaxFactory.ParenthesizedLambdaExpression()
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SeparatedList<ParameterSyntax>(
                                new ParameterSyntax[]
                                {
                                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("ct"))
                                        .WithType(SyntaxFactory.IdentifierName("CancellationToken")),
                                })))
                    .WithExpressionBody(createAsyncCall);
            }
            else
            {
                var newExpr = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList());

                lambda = SyntaxFactory.ParenthesizedLambdaExpression()
                    .WithParameterList(SyntaxFactory.ParameterList())
                    .WithExpressionBody(newExpr);
            }

            builderExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("Register")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(entry.Key))),
                            SyntaxFactory.Argument(lambda),
                        })));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));
    }

    private static ExpressionSyntax CreatePooledFactoryRegistryBuilderExpression(
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, bool ImplementsAsyncFactory)> entries,
        int poolSize)
    {
        var builderType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("AsyncFactoryRegistryBuilder"))
            .WithTypeArgumentList(
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(
                        new TypeSyntax[]
                        {
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            SyntaxFactory.ParseTypeName(contractTypeName),
                        })));

        ExpressionSyntax builderExpression = SyntaxFactory.ObjectCreationExpression(builderType)
            .WithArgumentList(SyntaxFactory.ArgumentList());

        // WithPooling(poolSize)
        builderExpression = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("WithPooling")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList<ArgumentSyntax>(
                    new ArgumentSyntax[]
                    {
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                SyntaxFactory.Literal(poolSize))),
                    })));

        foreach (var entry in entries)
        {
            ExpressionSyntax lambda;
            if (entry.ImplementsAsyncFactory)
            {
                var newExpr = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
                var createAsyncCall = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        newExpr,
                        SyntaxFactory.IdentifierName("CreateAsync")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList<ArgumentSyntax>(
                            new ArgumentSyntax[]
                            {
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("ct")),
                            })));

                lambda = SyntaxFactory.ParenthesizedLambdaExpression()
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SeparatedList<ParameterSyntax>(
                                new ParameterSyntax[]
                                {
                                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("ct"))
                                        .WithType(SyntaxFactory.IdentifierName("CancellationToken")),
                                })))
                    .WithExpressionBody(createAsyncCall);
            }
            else
            {
                var newExpr = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(entry.ImplementationTypeName))
                    .WithArgumentList(SyntaxFactory.ArgumentList());

                lambda = SyntaxFactory.ParenthesizedLambdaExpression()
                    .WithParameterList(SyntaxFactory.ParameterList())
                    .WithExpressionBody(newExpr);
            }

            builderExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    builderExpression,
                    SyntaxFactory.IdentifierName("Register")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList<ArgumentSyntax>(
                        new ArgumentSyntax[]
                        {
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(entry.Key))),
                            SyntaxFactory.Argument(lambda),
                        })));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                builderExpression,
                SyntaxFactory.IdentifierName("Build")));
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

    public static string GetAsyncRegistryClassName(string contractName) =>
        GetBaseName(contractName) + "AsyncRegistry";

    public static string GetPooledRegistryClassName(string contractName) =>
        GetBaseName(contractName) + "PooledRegistry";

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
