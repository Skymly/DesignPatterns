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

    public static string GetVisitorInterfaceName(string contractName) =>
        "I" + GetBaseName(contractName) + "NodeVisitor";

    public static string GetAsyncVisitorInterfaceName(string contractName) =>
        "I" + GetBaseName(contractName) + "NodeAsyncVisitor";

    public static string GetAsyncVisitorInterfaceNameFromVisitor(string visitorInterfaceName) =>
        visitorInterfaceName.Replace("NodeVisitor", "NodeAsyncVisitor");

    public static string GetGenericVisitorInterfaceName(string contractName) =>
        "I" + GetBaseName(contractName) + "NodeVisitor";

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
                $"The key for the {k.KeyValue} composite part."));

        var keysClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(keysClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members.ToArray()),
            "Provides registry keys for the composite contract.");

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, keysClass, "CompositePartGenerator");
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

        var buildRootMethod = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName(contractTypeName),
                    SyntaxFactory.Identifier("BuildRoot"))
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ReturnStatement(assembleInvocation)))),
            "Builds the composite tree root.");

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

        var buildForestMethod = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    forestReturnType,
                    SyntaxFactory.Identifier("BuildForest"))
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithBody(
                    SyntaxFactory.Block(
                        SyntaxFactory.SingletonList<StatementSyntax>(
                            SyntaxFactory.ReturnStatement(assembleForestInvocation)))),
            "Builds all composite tree roots (forest).");

        var catalogClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(catalogClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(entriesField, buildRootMethod, buildForestMethod),
            $"Provides a composite catalog for {contractTypeName}.");

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

        return GeneratedCodeHelper.WrapInCompilationUnit(
            namespaceName,
            catalogClass,
            "CompositePartGenerator",
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

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName(contractTypeName),
                    SyntaxFactory.Identifier("BuildRoot"))
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(spParam)
                .WithBody(body),
            "Builds the composite tree root from the service provider.");
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

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    forestReturnType,
                    SyntaxFactory.Identifier("BuildForest"))
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(spParam)
                .WithBody(body),
            "Builds all composite tree roots (forest) from the service provider.");
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

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                    SyntaxFactory.Identifier("RegisterDi"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(servicesParam, lifetimeParam)
                .WithBody(body),
            "Registers the registry and all implementations in the DI container.");
    }

    public static CompilationUnitSyntax CreateVisitorInterfaceCompilationUnit(
        string? namespaceName,
        string visitorInterfaceName,
        string contractTypeName,
        IReadOnlyList<(string ImplementationName, string? ImplementationNamespace, string ImplementationFullyQualifiedDisplayString)> implementations)
    {
        // void Visit(TImpl node) methods
        var voidVisitMethods = implementations.Select(impl =>
            GeneratedCodeHelper.WithXmlDoc(
                SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                        SyntaxFactory.Identifier("Visit"))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("node"))
                                    .WithType(SyntaxFactory.ParseTypeName(impl.ImplementationFullyQualifiedDisplayString)))))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                $"Visits a {impl.ImplementationName} node.")).ToArray();

        var voidVisitor = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.InterfaceDeclaration(visitorInterfaceName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(voidVisitMethods),
            $"Defines a visitor for {contractTypeName} composite nodes.");

        // async: ValueTask VisitAsync(TImpl node, CancellationToken ct)
        var asyncVisitorName = GetAsyncVisitorInterfaceNameFromVisitor(visitorInterfaceName);
        var asyncVisitMethods = implementations.Select(impl =>
            GeneratedCodeHelper.WithXmlDoc(
                SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.ValueTask"),
                        SyntaxFactory.Identifier("VisitAsync"))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
                            {
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("node"))
                                    .WithType(SyntaxFactory.ParseTypeName(impl.ImplementationFullyQualifiedDisplayString)),
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                                    .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken")),
                            })))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                $"Visits a {impl.ImplementationName} node asynchronously.")).ToArray();

        var asyncVisitor = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.InterfaceDeclaration(asyncVisitorName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(asyncVisitMethods),
            $"Defines an asynchronous visitor for {contractTypeName} composite nodes.");

        // generic TResult: TResult Visit<TResult>(TImpl node)
        var genericVisitMethods = implementations.Select(impl =>
            GeneratedCodeHelper.WithXmlDoc(
                SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.IdentifierName("TResult"),
                        SyntaxFactory.Identifier("Visit"))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("node"))
                                    .WithType(SyntaxFactory.ParseTypeName(impl.ImplementationFullyQualifiedDisplayString)))))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                $"Visits a {impl.ImplementationName} node and returns a result.")).ToArray();

        var genericVisitor = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.InterfaceDeclaration(visitorInterfaceName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .WithTypeParameterList(
                    SyntaxFactory.TypeParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.TypeParameter(SyntaxFactory.Identifier("TResult")))))
                .AddMembers(genericVisitMethods),
            $"Defines a generic visitor for {contractTypeName} composite nodes.");

        // Dispatch extension class — runtime type dispatch via pattern matching
        var dispatchClass = CreateDispatchExtensionsClass(
            visitorInterfaceName,
            asyncVisitorName,
            contractTypeName,
            implementations);

        var annotatedMembers = new TypeDeclarationSyntax[] { voidVisitor, asyncVisitor, genericVisitor, dispatchClass }
            .Select(GeneratedCodeHelper.AddGeneratedCodeAttribute)
            .Cast<MemberDeclarationSyntax>()
            .ToArray();

        var multiCompilationUnit = SyntaxFactory.CompilationUnit()
            .WithLeadingTrivia(GeneratedCodeHelper.CreateAutoGeneratedHeader("CompositePartGenerator"))
            .AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks")));

        var nullableTrivia = SyntaxFactory.ParseTrailingTrivia("#nullable enable")
            .FirstOrDefault();
        if (!nullableTrivia.IsKind(SyntaxKind.None))
        {
            multiCompilationUnit = multiCompilationUnit
                .WithTrailingTrivia(SyntaxFactory.TriviaList(nullableTrivia));
        }

        if (!string.IsNullOrEmpty(namespaceName))
        {
            var ns = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName!));
            foreach (var decl in annotatedMembers)
            {
                ns = ns.AddMembers(decl);
            }

            return multiCompilationUnit.AddMembers(ns).NormalizeWhitespace();
        }

        return multiCompilationUnit.AddMembers(annotatedMembers).NormalizeWhitespace();
    }

    private static ClassDeclarationSyntax CreateDispatchExtensionsClass(
        string visitorInterfaceName,
        string asyncVisitorInterfaceName,
        string contractTypeName,
        IReadOnlyList<(string ImplementationName, string? ImplementationNamespace, string ImplementationFullyQualifiedDisplayString)> implementations)
    {
        // void AcceptVisitor(this TContract node, IVisitor visitor) — runtime dispatch
        var voidDispatch = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("AcceptVisitor"))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("node"))
                                .WithType(SyntaxFactory.ParseTypeName(contractTypeName))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword))),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("visitor"))
                                .WithType(SyntaxFactory.ParseTypeName(visitorInterfaceName)),
                        })))
                .WithBody(CreateVoidDispatchBody(implementations)),
            $"Dispatches a {contractTypeName} node to the matching visitor overload.");

        // ValueTask AcceptVisitorAsync(this TContract node, IAsyncVisitor visitor, CancellationToken ct)
        var asyncDispatch = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.ParseTypeName("global::System.Threading.Tasks.ValueTask"),
                    SyntaxFactory.Identifier("AcceptVisitorAsync"))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("node"))
                                .WithType(SyntaxFactory.ParseTypeName(contractTypeName))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword))),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("visitor"))
                                .WithType(SyntaxFactory.ParseTypeName(asyncVisitorInterfaceName)),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                                .WithType(SyntaxFactory.ParseTypeName("global::System.Threading.CancellationToken")),
                        })))
                .WithBody(CreateAsyncDispatchBody(implementations)),
            $"Asynchronously dispatches a {contractTypeName} node to the matching visitor overload.");

        // TResult AcceptVisitor<TResult>(this TContract node, IVisitor<TResult> visitor)
        var genericDispatch = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.IdentifierName("TResult"),
                    SyntaxFactory.Identifier("AcceptVisitor"))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithTypeParameterList(
                    SyntaxFactory.TypeParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.TypeParameter(SyntaxFactory.Identifier("TResult")))))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("node"))
                                .WithType(SyntaxFactory.ParseTypeName(contractTypeName))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ThisKeyword))),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("visitor"))
                                .WithType(SyntaxFactory.ParseTypeName($"{visitorInterfaceName}<TResult>")),
                        })))
                .WithBody(CreateGenericDispatchBody(implementations)),
            $"Dispatches a {contractTypeName} node to the matching visitor overload and returns a result.");

        var baseName = visitorInterfaceName.StartsWith("I", StringComparison.Ordinal) && visitorInterfaceName.Length > 1
            ? visitorInterfaceName.Substring(1)
            : visitorInterfaceName;

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(baseName + "Extensions")
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(voidDispatch, asyncDispatch, genericDispatch),
            $"Provides visitor dispatch extensions for {contractTypeName}.");
    }

    private static BlockSyntax CreateVoidDispatchBody(
        IReadOnlyList<(string ImplementationName, string? ImplementationNamespace, string ImplementationFullyQualifiedDisplayString)> implementations)
    {
        var sb = new System.Text.StringBuilder("switch (node) { ");
        foreach (var impl in implementations)
        {
            sb.Append($"case {impl.ImplementationFullyQualifiedDisplayString} typed: visitor.Visit(typed); return; ");
        }

        sb.Append($"default: throw new global::System.InvalidOperationException($\"No visitor overload found for node type '{{node?.GetType().FullName}}'.\"); }}");

        return SyntaxFactory.Block(SyntaxFactory.ParseStatement(sb.ToString()));
    }

    private static BlockSyntax CreateAsyncDispatchBody(
        IReadOnlyList<(string ImplementationName, string? ImplementationNamespace, string ImplementationFullyQualifiedDisplayString)> implementations)
    {
        var sb = new System.Text.StringBuilder("switch (node) { ");
        foreach (var impl in implementations)
        {
            sb.Append($"case {impl.ImplementationFullyQualifiedDisplayString} typed: return visitor.VisitAsync(typed, cancellationToken); ");
        }

        sb.Append($"default: throw new global::System.InvalidOperationException($\"No visitor overload found for node type '{{node?.GetType().FullName}}'.\"); }}");

        return SyntaxFactory.Block(SyntaxFactory.ParseStatement(sb.ToString()));
    }

    private static BlockSyntax CreateGenericDispatchBody(
        IReadOnlyList<(string ImplementationName, string? ImplementationNamespace, string ImplementationFullyQualifiedDisplayString)> implementations)
    {
        var sb = new System.Text.StringBuilder("switch (node) { ");
        foreach (var impl in implementations)
        {
            sb.Append($"case {impl.ImplementationFullyQualifiedDisplayString} typed: return visitor.Visit(typed); ");
        }

        sb.Append($"default: throw new global::System.InvalidOperationException($\"No visitor overload found for node type '{{node?.GetType().FullName}}'.\"); }}");

        return SyntaxFactory.Block(SyntaxFactory.ParseStatement(sb.ToString()));
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
