using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.SourceGenerators.Syntax;

internal static class StateTransitionSyntaxFactory
{
    public static string GetTransitionTableClassName(INamedTypeSymbol stateType) =>
        GetTransitionTableClassName(stateType.Name);

    public static string GetTransitionTableClassName(string stateTypeName) =>
        stateTypeName + "TransitionTable";

    public static CompilationUnitSyntax CreateTransitionTableCompilationUnit(
        string? namespaceName,
        string tableClassName,
        string stateTypeName,
        string triggerTypeName,
        string initialStateExpression,
        IReadOnlyList<(string FromExpression, string TriggerExpression, string ToExpression, string? GuardExpression)> transitions)
    {
        var tableExpression = BuildTransitionTableExpression(
            stateTypeName,
            triggerTypeName,
            initialStateExpression,
            transitions);

        var members = new MemberDeclarationSyntax[]
        {
            SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.GenericName(
                                    SyntaxFactory.Identifier("ITransitionTable"))
                                .WithTypeArgumentList(
                                    SyntaxFactory.TypeArgumentList(
                                        SyntaxFactory.SeparatedList<TypeSyntax>(
                                            new TypeSyntax[]
                                            {
                                                SyntaxFactory.ParseTypeName(stateTypeName),
                                                SyntaxFactory.ParseTypeName(triggerTypeName),
                                            }))))
                        .AddVariables(
                            SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("Table"))
                                .WithInitializer(
                                    SyntaxFactory.EqualsValueClause(tableExpression))))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))),
            SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.ParseTypeName(tableClassName),
                    SyntaxFactory.Identifier("Instance"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithAccessorList(
                    SyntaxFactory.AccessorList(
                        SyntaxFactory.SingletonList(
                            SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                .WithExpressionBody(
                                    SyntaxFactory.ArrowExpressionClause(
                                        SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(tableClassName))
                                            .WithArgumentList(SyntaxFactory.ArgumentList())))
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))))),
            CreateForwardingProperty("InitialState", stateTypeName, "Table.InitialState"),
            CreateForwardingMethod(
                "TryTransition",
                SyntaxFactory.ParseTypeName("bool"),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel(stateTypeName, "next", isOut: true),
                },
                "Table.TryTransition(current, trigger, out next)"),
            CreateForwardingMethod(
                "GetAllowedTriggers",
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("IReadOnlyList"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.ParseTypeName(triggerTypeName)))),
                new[] { new ParameterModel(stateTypeName, "current") },
                "Table.GetAllowedTriggers(current)"),
            CreateForwardingMethod(
                "CanTransitionFrom",
                SyntaxFactory.ParseTypeName("bool"),
                new[] { new ParameterModel(stateTypeName, "current") },
                "Table.CanTransitionFrom(current)"),
        };

        var tableClass = SyntaxFactory.ClassDeclaration(tableClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.SealedKeyword)))
            .AddBaseListTypes(
                SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("ITransitionTable"))
                        .WithTypeArgumentList(
                            SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SeparatedList<TypeSyntax>(
                                    new TypeSyntax[]
                                    {
                                        SyntaxFactory.ParseTypeName(stateTypeName),
                                        SyntaxFactory.ParseTypeName(triggerTypeName),
                                    })))))
            .AddMembers(members);

        return WrapInCompilationUnit(namespaceName, tableClass, "System", "System.Collections.Generic", "DesignPatterns.Behavioral");
    }

    public static CompilationUnitSyntax CreateHolderPartialCompilationUnit(
        string? namespaceName,
        string holderClassName,
        string tableClassName,
        string stateTypeName,
        string triggerTypeName)
    {
        var members = new MemberDeclarationSyntax[]
        {
            CreateForwardingProperty(
                "InitialState",
                stateTypeName,
                $"{tableClassName}.Instance.InitialState",
                isStatic: true),
            CreateForwardingMethod(
                "TryTransition",
                SyntaxFactory.ParseTypeName("bool"),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel(stateTypeName, "next", isOut: true),
                },
                $"{tableClassName}.Instance.TryTransition(current, trigger, out next)",
                isStatic: true),
        };

        var holderClass = SyntaxFactory.ClassDeclaration(holderClassName)
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .AddMembers(members);

        return WrapInCompilationUnit(namespaceName, holderClass, "DesignPatterns.Behavioral");
    }

    public static string FormatEnumMember(INamedTypeSymbol enumType, string memberName) =>
        FormatEnumMember(enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), memberName);

    public static string FormatEnumMember(string enumTypeFullyQualifiedDisplayString, string memberName) =>
        $"{enumTypeFullyQualifiedDisplayString}.{memberName}";

    private static ExpressionSyntax BuildTransitionTableExpression(
        string stateTypeName,
        string triggerTypeName,
        string initialStateExpression,
        IReadOnlyList<(string FromExpression, string TriggerExpression, string ToExpression, string? GuardExpression)> transitions)
    {
        var builder = new StringBuilder();
        builder.Append("new TransitionTableBuilder<")
            .Append(stateTypeName)
            .Append(", ")
            .Append(triggerTypeName)
            .Append(">().WithInitial(")
            .Append(initialStateExpression)
            .Append(')');

        foreach (var transition in transitions)
        {
            builder.Append(".Add(")
                .Append(transition.FromExpression)
                .Append(", ")
                .Append(transition.TriggerExpression)
                .Append(", ")
                .Append(transition.ToExpression);

            if (transition.GuardExpression is not null)
            {
                builder.Append(", guard: ")
                    .Append(transition.GuardExpression);
            }

            builder.Append(')');
        }

        builder.Append(".Build()");
        return SyntaxFactory.ParseExpression(builder.ToString());
    }

    private static PropertyDeclarationSyntax CreateForwardingProperty(
        string name,
        string propertyTypeName,
        string expression,
        bool isStatic = false)
    {
        var modifiers = isStatic
            ? new[] { SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword }
            : new[] { SyntaxKind.PublicKeyword };

        return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(propertyTypeName), SyntaxFactory.Identifier(name))
            .WithModifiers(SyntaxFactory.TokenList(modifiers.Select(SyntaxFactory.Token)))
            .WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(expression)))
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));
    }

    private static MethodDeclarationSyntax CreateForwardingMethod(
        string name,
        TypeSyntax returnType,
        IReadOnlyList<ParameterModel> parameters,
        string expression,
        bool isStatic = false)
    {
        var modifiers = isStatic
            ? new[] { SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword }
            : new[] { SyntaxKind.PublicKeyword };

        return SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier(name))
            .WithModifiers(SyntaxFactory.TokenList(modifiers.Select(SyntaxFactory.Token)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList(
                        parameters.Select(parameter =>
                        {
                            var parameterSyntax = SyntaxFactory.Parameter(
                                    SyntaxFactory.Identifier(parameter.ParameterName))
                                .WithType(SyntaxFactory.ParseTypeName(parameter.TypeName));

                            if (parameter.IsOut)
                            {
                                parameterSyntax = parameterSyntax.WithModifiers(
                                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.OutKeyword)));
                            }

                            return parameterSyntax;
                        }))))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(expression)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private readonly struct ParameterModel
    {
        public ParameterModel(string typeName, string parameterName, bool isOut = false)
        {
            TypeName = typeName;
            ParameterName = parameterName;
            IsOut = isOut;
        }

        public string TypeName { get; }

        public string ParameterName { get; }

        public bool IsOut { get; }
    }

    private static CompilationUnitSyntax WrapInCompilationUnit(
        string? namespaceName,
        TypeDeclarationSyntax typeDeclaration,
        params string[] additionalUsings)
    {
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithLeadingTrivia(CreateAutoGeneratedHeader());

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
            SyntaxFactory.Comment("// Generated by DesignPatterns.SourceGenerators.StateTransitionGenerator"),
            SyntaxFactory.EndOfLine("\n"));
}
