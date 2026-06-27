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
                $"The key for the {k.KeyValue} strategy."));

        var keysClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(keysClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            "Provides registry keys for the strategy contract.");

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, keysClass, "RegisterStrategyGenerator");
    }

    public static CompilationUnitSyntax CreateRegistryCompilationUnit(
        string? namespaceName,
        string registryClassName,
        string contractTypeName,
        IReadOnlyList<(string Key, string ImplementationTypeName, string? GuardMethodReference)> entries,
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

        // Build guard dictionary when any entry has a guard method reference.
        var guardedEntries = entries
            .Where(static e => !string.IsNullOrEmpty(e.GuardMethodReference))
            .ToList();

        ArgumentListSyntax registryConstructorArgs;
        if (guardedEntries.Count > 0)
        {
            var guardDictType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("Dictionary"))
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList<TypeSyntax>(
                            new TypeSyntax[]
                            {
                                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                                SyntaxFactory.ParseTypeName("global::System.Func<string, bool>"),
                            })));

            var guardDictInitializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SeparatedList<ExpressionSyntax>(
                    guardedEntries.Select(e =>
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
                            SyntaxFactory.ParseExpression(e.GuardMethodReference!)))));

            var guardDictCreation = SyntaxFactory.ObjectCreationExpression(guardDictType)
                .WithInitializer(guardDictInitializer);

            registryConstructorArgs = SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                    new[]
                    {
                        SyntaxFactory.Argument(dictionaryCreation),
                        SyntaxFactory.Argument(guardDictCreation),
                    }));
        }
        else
        {
            registryConstructorArgs = SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(dictionaryCreation)));
        }

        var registryInstanceCreation = SyntaxFactory.ObjectCreationExpression(strategyRegistryType)
            .WithArgumentList(registryConstructorArgs);

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

        var instanceProperty = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.PropertyDeclaration(
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
                                SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("_instance")))))),
            "Gets the singleton registry instance.");

        var members = new List<MemberDeclarationSyntax> { registryField, instanceProperty };

        // Project to 2-tuple for helpers that don't need guard info.
        var entriesWithoutGuard = entries
            .Select(static e => (e.Key, e.ImplementationTypeName))
            .ToList();

        if (integrationOptions.NeedsRegistrationEntries)
        {
            members.Add(DiIntegrationSyntaxHelper.CreateDiEntriesField(entriesWithoutGuard));
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

        var registryClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(registryClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            $"Provides a registry for {contractTypeName}.");

        var additionalUsings = new List<string> { "System.Collections.Generic", "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi)
        {
            additionalUsings.AddRange(DiIntegrationSyntaxHelper.GetDiUsings());
        }

        if (integrationOptions.EnableAutofac)
        {
            additionalUsings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, registryClass, "RegisterStrategyGenerator", additionalUsings.ToArray());
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
