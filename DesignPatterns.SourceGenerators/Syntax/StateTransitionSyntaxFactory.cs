using System.Collections.Generic;
using System.Linq;
using System.Text;
using DesignPatterns.SourceGenerators;
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

    public static string GetStateMachineClassName(string stateTypeName) =>
        stateTypeName + "StateMachine";

    public static CompilationUnitSyntax CreateTransitionTableCompilationUnit(
        string? namespaceName,
        string tableClassName,
        string stateTypeName,
        string triggerTypeName,
        string initialStateExpression,
        IReadOnlyList<(
            string FromExpression,
            string TriggerExpression,
            string ToExpression,
            string? GuardExpression,
            string? OnEnterSyncReference,
            string? OnExitSyncReference,
            string? OnEnterAsyncReference,
            string? OnExitAsyncReference)> transitions,
        GeneratorIntegrationOptions integrationOptions)
    {
        var tableExpression = BuildTransitionTableExpression(
            stateTypeName,
            triggerTypeName,
            initialStateExpression,
            transitions);

        var members = new List<MemberDeclarationSyntax>
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
            GeneratedCodeHelper.WithXmlDoc(
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
                "Gets the singleton transition table instance."),
            CreateForwardingProperty(
                "InitialState",
                stateTypeName,
                "Table.InitialState",
                summary: "Gets the initial state of the state machine."),
            CreateForwardingMethod(
                "TryTransition",
                SyntaxFactory.ParseTypeName("bool"),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel(stateTypeName, "next", isOut: true),
                },
                "Table.TryTransition(current, trigger, out next)",
                summary: "Attempts a state transition from the current state with the specified trigger."),
            CreateForwardingMethod(
                "TryTransitionAsync",
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("ValueTask"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("TransitionResult"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.ParseTypeName(stateTypeName))))))),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel("CancellationToken", "cancellationToken"),
                },
                "Table.TryTransitionAsync(current, trigger, cancellationToken)",
                summary: "Attempts an asynchronous state transition."),
            CreateForwardingMethod(
                "TryTransitionTracedAsync",
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("ValueTask"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("TransitionTrace"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.ParseTypeName(stateTypeName))))))),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel("CancellationToken", "cancellationToken"),
                },
                "Table.TryTransitionTracedAsync(current, trigger, cancellationToken)",
                summary: "Attempts an asynchronous state transition with execution tracing."),
            CreateForwardingMethod(
                "GetAllowedTriggers",
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("IReadOnlyList"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.ParseTypeName(triggerTypeName)))),
                new[] { new ParameterModel(stateTypeName, "current") },
                "Table.GetAllowedTriggers(current)",
                summary: "Gets the triggers allowed from the specified state."),
            CreateForwardingMethod(
                "CanTransitionFrom",
                SyntaxFactory.ParseTypeName("bool"),
                new[] { new ParameterModel(stateTypeName, "current") },
                "Table.CanTransitionFrom(current)",
                summary: "Determines whether any transition is possible from the specified state."),
        };

        if (integrationOptions.EnableDi)
        {
            members.Add(CreateRegisterDiMethod(stateTypeName, triggerTypeName));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(CreateTableRegisterAutofacMethod(stateTypeName, triggerTypeName));
        }

        var tableClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(tableClassName)
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
                .AddMembers(members.ToArray()),
            $"Provides a transition table for {stateTypeName}.");

        var usings = new List<string> { "System", "System.Collections.Generic", "System.Threading", "System.Threading.Tasks", "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi)
        {
            usings.Add("Microsoft.Extensions.DependencyInjection");
            usings.Add("Microsoft.Extensions.DependencyInjection.Extensions");
        }

        if (integrationOptions.EnableAutofac)
        {
            usings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, tableClass, "StateTransitionGenerator", usings.ToArray());
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
                isStatic: true,
                summary: "Gets the initial state of the state machine."),
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
                isStatic: true,
                summary: "Attempts a state transition from the current state with the specified trigger."),
            CreateForwardingMethod(
                "TryTransitionAsync",
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("ValueTask"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("TransitionResult"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.ParseTypeName(stateTypeName))))))),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel("CancellationToken", "cancellationToken"),
                },
                $"{tableClassName}.Instance.TryTransitionAsync(current, trigger, cancellationToken)",
                isStatic: true,
                summary: "Attempts an asynchronous state transition."),
            CreateForwardingMethod(
                "TryTransitionTracedAsync",
                SyntaxFactory.GenericName(SyntaxFactory.Identifier("ValueTask"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("TransitionTrace"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(
                                                SyntaxFactory.ParseTypeName(stateTypeName))))))),
                new[]
                {
                    new ParameterModel(stateTypeName, "current"),
                    new ParameterModel(triggerTypeName, "trigger"),
                    new ParameterModel("CancellationToken", "cancellationToken"),
                },
                $"{tableClassName}.Instance.TryTransitionTracedAsync(current, trigger, cancellationToken)",
                isStatic: true,
                summary: "Attempts an asynchronous state transition with execution tracing."),
        };

        var holderClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(holderClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .AddMembers(members),
            $"Provides static accessors for the {stateTypeName} state machine.");

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, holderClass, "StateTransitionGenerator", "System.Threading", "System.Threading.Tasks", "DesignPatterns.Behavioral");
    }

    public static CompilationUnitSyntax CreateStateMachineCompilationUnit(
        string? namespaceName,
        string stateMachineClassName,
        string tableClassName,
        string stateTypeName,
        string triggerTypeName,
        GeneratorIntegrationOptions integrationOptions)
    {
        // Parameterless constructor: calls base(tableClassName.Instance)
        var parameterlessConstructor = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(stateMachineClassName))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithInitializer(
                    SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(
                                        SyntaxFactory.MemberAccessExpression(
                                            SyntaxKind.SimpleMemberAccessExpression,
                                            SyntaxFactory.IdentifierName(tableClassName),
                                            SyntaxFactory.IdentifierName("Instance")))))))
                .WithBody(SyntaxFactory.Block()),
            $"Initializes a new instance of the {stateMachineClassName} class.");

        // DI constructor: accepts ITransitionTable<TState, TTrigger>
        var tableInterfaceType = $"ITransitionTable<{stateTypeName}, {triggerTypeName}>";
        var diConstructor = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ConstructorDeclaration(SyntaxFactory.Identifier(stateMachineClassName))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("table"))
                                .WithType(SyntaxFactory.ParseTypeName(tableInterfaceType)))))
                .WithInitializer(
                    SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("table"))))))
                .WithBody(SyntaxFactory.Block()),
            $"Initializes a new instance of the {stateMachineClassName} class with the specified transition table.");

        var members = new List<MemberDeclarationSyntax> { parameterlessConstructor, diConstructor };

        if (integrationOptions.EnableDi)
        {
            members.Add(CreateStateMachineRegisterDiMethod(stateTypeName, triggerTypeName, tableClassName, stateMachineClassName));
        }

        if (integrationOptions.EnableAutofac)
        {
            members.Add(CreateStateMachineRegisterAutofacMethod(stateTypeName, triggerTypeName, tableClassName, stateMachineClassName));
        }

        var stateMachineClass = GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.ClassDeclaration(stateMachineClassName)
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.SealedKeyword)))
                .AddBaseListTypes(
                    SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("StateMachine"))
                            .WithTypeArgumentList(
                                SyntaxFactory.TypeArgumentList(
                                    SyntaxFactory.SeparatedList<TypeSyntax>(
                                        new TypeSyntax[]
                                        {
                                            SyntaxFactory.ParseTypeName(stateTypeName),
                                            SyntaxFactory.ParseTypeName(triggerTypeName),
                                        })))))
                .AddMembers(members.ToArray()),
            $"Provides a state machine for {stateTypeName}.");

        var usings = new List<string> { "DesignPatterns.Behavioral" };
        if (integrationOptions.EnableDi)
        {
            usings.Add("Microsoft.Extensions.DependencyInjection");
            usings.Add("Microsoft.Extensions.DependencyInjection.Extensions");
        }

        if (integrationOptions.EnableAutofac)
        {
            usings.AddRange(AutofacIntegrationSyntaxHelper.GetAutofacUsings());
        }

        return GeneratedCodeHelper.WrapInCompilationUnit(namespaceName, stateMachineClass, "StateTransitionGenerator", usings.ToArray());
    }

    public static string FormatEnumMember(INamedTypeSymbol enumType, string memberName) =>
        FormatEnumMember(enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), memberName);

    public static string FormatEnumMember(string enumTypeFullyQualifiedDisplayString, string memberName) =>
        $"{enumTypeFullyQualifiedDisplayString}.{memberName}";

    private static ExpressionSyntax BuildTransitionTableExpression(
        string stateTypeName,
        string triggerTypeName,
        string initialStateExpression,
        IReadOnlyList<(
            string FromExpression,
            string TriggerExpression,
            string ToExpression,
            string? GuardExpression,
            string? OnEnterSyncReference,
            string? OnExitSyncReference,
            string? OnEnterAsyncReference,
            string? OnExitAsyncReference)> transitions)
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

            var hasGuard = transition.GuardExpression is not null;
            var hasOnEnterSync = transition.OnEnterSyncReference is not null;
            var hasOnExitSync = transition.OnExitSyncReference is not null;
            var hasOnEnterAsync = transition.OnEnterAsyncReference is not null;
            var hasOnExitAsync = transition.OnExitAsyncReference is not null;
            var hasAnyOption = hasGuard || hasOnEnterSync || hasOnExitSync || hasOnEnterAsync || hasOnExitAsync;

            if (hasAnyOption)
            {
                // Guard is the 4th positional parameter; actions are named.
                if (hasGuard)
                {
                    builder.Append(", guard: ")
                        .Append(transition.GuardExpression);
                }

                if (hasOnEnterSync)
                {
                    builder.Append(", onEnterSync: ")
                        .Append(transition.OnEnterSyncReference);
                }

                if (hasOnExitSync)
                {
                    builder.Append(", onExitSync: ")
                        .Append(transition.OnExitSyncReference);
                }

                if (hasOnEnterAsync)
                {
                    builder.Append(", onEnterAsync: ")
                        .Append(transition.OnEnterAsyncReference);
                }

                if (hasOnExitAsync)
                {
                    builder.Append(", onExitAsync: ")
                        .Append(transition.OnExitAsyncReference);
                }
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
        bool isStatic = false,
        string? summary = null)
    {
        var modifiers = isStatic
            ? new[] { SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword }
            : new[] { SyntaxKind.PublicKeyword };

        var property = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(propertyTypeName), SyntaxFactory.Identifier(name))
            .WithModifiers(SyntaxFactory.TokenList(modifiers.Select(SyntaxFactory.Token)))
            .WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(expression)))
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))));

        return summary is null ? property : GeneratedCodeHelper.WithXmlDoc(property, summary);
    }

    private static MethodDeclarationSyntax CreateForwardingMethod(
        string name,
        TypeSyntax returnType,
        IReadOnlyList<ParameterModel> parameters,
        string expression,
        bool isStatic = false,
        string? summary = null)
    {
        var modifiers = isStatic
            ? new[] { SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword }
            : new[] { SyntaxKind.PublicKeyword };

        var method = SyntaxFactory.MethodDeclaration(returnType, SyntaxFactory.Identifier(name))
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

        return summary is null ? method : GeneratedCodeHelper.WithXmlDoc(method, summary);
    }

    private static MethodDeclarationSyntax CreateStateMachineRegisterDiMethod(
        string stateTypeName,
        string triggerTypeName,
        string tableClassName,
        string stateMachineClassName)
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

        var interfaceType = $"IStateMachine<{stateTypeName}, {triggerTypeName}>";

        var body = SyntaxFactory.Block(
            SyntaxFactory.ParseStatement(
                $"{tableClassName}.RegisterDi(services, lifetime);"),
            SyntaxFactory.ParseStatement(
                $"services.TryAdd(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof({interfaceType}), sp => new {stateMachineClassName}((ITransitionTable<{stateTypeName}, {triggerTypeName}>)sp.GetRequiredService(typeof(ITransitionTable<{stateTypeName}, {triggerTypeName}>))), lifetime));"),
            SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("services")));

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

    private static MethodDeclarationSyntax CreateRegisterDiMethod(
        string stateTypeName,
        string triggerTypeName)
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

        var interfaceType = $"ITransitionTable<{stateTypeName}, {triggerTypeName}>";

        var body = SyntaxFactory.Block(
            SyntaxFactory.ParseStatement(
                $"services.TryAdd(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof({interfaceType}), _ => Instance, lifetime));"),
            SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("services")));

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

    private static MethodDeclarationSyntax CreateTableRegisterAutofacMethod(
        string stateTypeName,
        string triggerTypeName)
    {
        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ContainerBuilder"));

        var sharingParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("sharing"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"),
                        SyntaxFactory.IdentifierName("Shared"))));

        var serviceKeyParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceKey"))
            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))
            .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));

        var interfaceType = $"ITransitionTable<{stateTypeName}, {triggerTypeName}>";

        // Tables are immutable/stateless — always registered as singleton.
        // The sharing parameter is accepted for API symmetry but ignored.
        var body = SyntaxFactory.Block(
            SyntaxFactory.ParseStatement(
                $"builder.Register(_ => Instance).As<{interfaceType}>().SingleInstance();"),
            SyntaxFactory.ParseStatement(
                $"if (serviceKey is not null) {{ builder.Register(_ => Instance).Keyed<{interfaceType}>(serviceKey).SingleInstance(); }}"));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("RegisterAutofac"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam, sharingParam, serviceKeyParam)
                .WithBody(body),
            "Registers the registry and all implementations with Autofac.");
    }

    private static MethodDeclarationSyntax CreateStateMachineRegisterAutofacMethod(
        string stateTypeName,
        string triggerTypeName,
        string tableClassName,
        string stateMachineClassName)
    {
        var builderParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("builder"))
            .WithType(SyntaxFactory.ParseTypeName("global::Autofac.ContainerBuilder"));

        var sharingParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("sharing"))
            .WithType(SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"))
            .WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"),
                        SyntaxFactory.IdentifierName("Shared"))));

        var serviceKeyParam = SyntaxFactory.Parameter(SyntaxFactory.Identifier("serviceKey"))
            .WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))
            .WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));

        var tableInterfaceType = $"ITransitionTable<{stateTypeName}, {triggerTypeName}>";
        var machineInterfaceType = $"IStateMachine<{stateTypeName}, {triggerTypeName}>";

        // Register the table first (always singleton), then the state machine.
        // State machines are stateful — respect the sharing parameter.
        var sharedRegistration = SyntaxFactory.ParseStatement(
            $"builder.Register(ctx => new {stateMachineClassName}(ctx.Resolve<{tableInterfaceType}>())).As<{machineInterfaceType}>().SingleInstance();");

        var transientRegistration = SyntaxFactory.ParseStatement(
            $"builder.Register(ctx => new {stateMachineClassName}(ctx.Resolve<{tableInterfaceType}>())).As<{machineInterfaceType}>().InstancePerDependency();");

        var sharedCondition = SyntaxFactory.BinaryExpression(
            SyntaxKind.EqualsExpression,
            SyntaxFactory.IdentifierName("sharing"),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseTypeName("global::DesignPatterns.Extensions.Autofac.InstanceSharing"),
                SyntaxFactory.IdentifierName("Shared")));

        var body = SyntaxFactory.Block(
            SyntaxFactory.ParseStatement($"{tableClassName}.RegisterAutofac(builder, sharing, serviceKey);"),
            SyntaxFactory.IfStatement(
                sharedCondition,
                SyntaxFactory.Block(sharedRegistration),
                SyntaxFactory.ElseClause(SyntaxFactory.Block(transientRegistration))));

        return GeneratedCodeHelper.WithXmlDoc(
            SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("RegisterAutofac"))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .AddParameterListParameters(builderParam, sharingParam, serviceKeyParam)
                .WithBody(body),
            "Registers the registry and all implementations with Autofac.");
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

}
