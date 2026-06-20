using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates transition tables for classes marked with <c>[StateMachine]</c>.
/// </summary>
[Generator]
public sealed class StateTransitionGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for <c>StateMachineAttribute</c>.</summary>
    public const string StateMachineMetadataName = "DesignPatterns.Behavioral.StateMachineAttribute";

    /// <summary>Metadata name for <c>TransitionAttribute</c>.</summary>
    public const string TransitionMetadataName = "DesignPatterns.Behavioral.TransitionAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var machines = context.SyntaxProvider.ForAttributeWithMetadataName(
            StateMachineMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx));

        context.RegisterSourceOutput(machines.Collect(), Execute);
    }

    private static StateMachineModel? Transform(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol holderType)
        {
            return null;
        }

        var stateMachineAttribute = context.Attributes.FirstOrDefault(attribute =>
            attribute.AttributeClass is not null &&
            string.Equals(
                attribute.AttributeClass.Name,
                "StateMachineAttribute",
                StringComparison.Ordinal));

        if (stateMachineAttribute is null)
        {
            return null;
        }

        var location = context.TargetNode.GetLocation();
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
        var isValidHolder = classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword)
            && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        INamedTypeSymbol? stateType = null;
        INamedTypeSymbol? triggerType = null;
        if (stateMachineAttribute.ConstructorArguments.Length >= 2)
        {
            stateType = stateMachineAttribute.ConstructorArguments[0].Value as INamedTypeSymbol;
            triggerType = stateMachineAttribute.ConstructorArguments[1].Value as INamedTypeSymbol;
        }

        TypedConstant? initial = null;
        foreach (var namedArgument in stateMachineAttribute.NamedArguments)
        {
            if (namedArgument.Key == "Initial")
            {
                initial = namedArgument.Value;
            }
        }

        var holderInfo = new ContractInfo(
            holderType.ToDisplayString(),
            holderType.Name,
            holderType.ContainingNamespace.IsGlobalNamespace
                ? null
                : holderType.ContainingNamespace.ToDisplayString(),
            holderType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        EnumTypeInfo? stateTypeInfo = null;
        EnumTypeInfo? triggerTypeInfo = null;
        var initialState = default(InitialStateInfo);

        if (stateType is not null && stateType.TypeKind == TypeKind.Enum)
        {
            stateTypeInfo = EnumTypeInfo.FromSymbol(stateType);
            initialState = ResolveInitial(initial, stateType, stateTypeInfo.Value);
        }

        if (triggerType is not null && triggerType.TypeKind == TypeKind.Enum)
        {
            triggerTypeInfo = EnumTypeInfo.FromSymbol(triggerType);
        }

        var transitions = new List<TransitionModel>();
        foreach (var attribute in holderType.GetAttributes())
        {
            if (attribute.AttributeClass is null
                || !string.Equals(attribute.AttributeClass.Name, "TransitionAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length < 3)
            {
                continue;
            }

            var transitionLocation = attribute.ApplicationSyntaxReference is { } syntaxReference
                ? syntaxReference.SyntaxTree!.GetLocation(syntaxReference.Span)
                : location;

            var from = ResolveTransitionArg(attribute.ConstructorArguments[0], stateType, stateTypeInfo);
            var trigger = ResolveTransitionArg(attribute.ConstructorArguments[1], triggerType, triggerTypeInfo);
            var to = ResolveTransitionArg(attribute.ConstructorArguments[2], stateType, stateTypeInfo);

            transitions.Add(new TransitionModel(from, trigger, to, transitionLocation));
        }

        return new StateMachineModel(
            holderInfo,
            stateTypeInfo,
            triggerTypeInfo,
            initialState,
            new EquatableArray<TransitionModel>(transitions.ToArray()),
            location,
            isValidHolder);
    }

    private static InitialStateInfo ResolveInitial(
        TypedConstant? initial,
        INamedTypeSymbol stateType,
        EnumTypeInfo stateTypeInfo)
    {
        if (!initial.HasValue)
        {
            return new InitialStateInfo(null, "<missing>", false);
        }

        var typedConstant = initial.Value;
        if (typedConstant.Kind != TypedConstantKind.Enum
            || typedConstant.Type is not INamedTypeSymbol constantEnumType
            || !SymbolEqualityComparer.Default.Equals(constantEnumType, stateType))
        {
            return new InitialStateInfo(null, typedConstant.ToCSharpString(), false);
        }

        foreach (var member in stateTypeInfo.Members)
        {
            if (Equals(member.ConstantValue, typedConstant.Value))
            {
                return new InitialStateInfo(member.Name, member.Name, true);
            }
        }

        return new InitialStateInfo(null, typedConstant.ToCSharpString(), false);
    }

    private static TransitionArg ResolveTransitionArg(
        TypedConstant constant,
        INamedTypeSymbol? expectedEnumType,
        EnumTypeInfo? expectedEnumInfo)
    {
        if (expectedEnumType is null || expectedEnumInfo is null)
        {
            return new TransitionArg(null, constant.ToCSharpString(), false);
        }

        if (constant.Kind == TypedConstantKind.Error || constant.Value is null)
        {
            return new TransitionArg(null, "<missing>", false);
        }

        if (constant.Kind != TypedConstantKind.Enum
            || constant.Type is not INamedTypeSymbol constantEnumType
            || !SymbolEqualityComparer.Default.Equals(constantEnumType, expectedEnumType))
        {
            return new TransitionArg(null, constant.ToCSharpString(), false);
        }

        foreach (var member in expectedEnumInfo.Value.Members)
        {
            if (Equals(member.ConstantValue, constant.Value))
            {
                return new TransitionArg(member.Name, member.Name, true);
            }
        }

        return new TransitionArg(null, constant.ToCSharpString(), false);
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<StateMachineModel?> models)
    {
        foreach (var model in models.Where(static model => model is not null))
        {
            ProcessModel(context, model!);
        }
    }

    private static void ProcessModel(SourceProductionContext context, StateMachineModel model)
    {
        if (!model.IsValidHolder)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateMachineHolderInvalid,
                model.Location,
                model.Holder.Name));
            return;
        }

        if (model.StateType is null || model.TriggerType is null)
        {
            return;
        }

        var stateType = model.StateType.Value;
        var triggerType = model.TriggerType.Value;

        if (!model.Initial.IsValid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionInvalidInitialState,
                model.Location,
                model.Initial.DisplayValue,
                stateType.FullyQualifiedName));
            return;
        }

        var initialMember = model.Initial.MemberName!;
        var validTransitions = new List<ResolvedTransition>();
        foreach (var transition in model.Transitions)
        {
            if (!transition.From.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidStateMember,
                    transition.Location,
                    transition.From.DisplayValue,
                    stateType.FullyQualifiedName));
                continue;
            }

            if (!transition.To.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidStateMember,
                    transition.Location,
                    transition.To.DisplayValue,
                    stateType.FullyQualifiedName));
                continue;
            }

            if (!transition.Trigger.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidTriggerMember,
                    transition.Location,
                    transition.Trigger.DisplayValue,
                    triggerType.FullyQualifiedName));
                continue;
            }

            validTransitions.Add(new ResolvedTransition(
                transition.From.MemberName!,
                transition.Trigger.MemberName!,
                transition.To.MemberName!,
                transition.Location,
                StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, transition.From.MemberName!),
                StateTransitionSyntaxFactory.FormatEnumMember(triggerType.FullyQualifiedDisplayString, transition.Trigger.MemberName!),
                StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, transition.To.MemberName!)));
        }

        foreach (var duplicateGroup in validTransitions
                     .GroupBy(static transition => (transition.FromMember, transition.TriggerMember))
                     .Where(static group => group.Count() > 1))
        {
            foreach (var transition in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionDuplicateEdge,
                    transition.Location,
                    transition.FromMember,
                    transition.TriggerMember));
            }
        }

        var uniqueTransitions = validTransitions
            .GroupBy(static transition => (transition.FromMember, transition.TriggerMember))
            .Select(static group => group.First())
            .ToList();

        ReportIsolatedStates(context, stateType, initialMember, uniqueTransitions, model.Location);

        if (uniqueTransitions.Count == 0 && model.Transitions.Count > 0)
        {
            return;
        }

        var tableClassName = StateTransitionSyntaxFactory.GetTransitionTableClassName(stateType.Name);
        var initialExpression = StateTransitionSyntaxFactory.FormatEnumMember(stateType.FullyQualifiedDisplayString, initialMember);
        var transitionExpressions = uniqueTransitions
            .Select(static transition => (transition.FromExpression, transition.TriggerExpression, transition.ToExpression))
            .ToList();

        // Generated sources go into the holder's namespace (not the state enum's namespace)
        // so the holder partial merges with the user-written partial declaration.
        var namespaceName = model.Holder.Namespace;

        var tableUnit = StateTransitionSyntaxFactory.CreateTransitionTableCompilationUnit(
            namespaceName,
            tableClassName,
            stateType.FullyQualifiedDisplayString,
            triggerType.FullyQualifiedDisplayString,
            initialExpression,
            transitionExpressions);

        var holderUnit = StateTransitionSyntaxFactory.CreateHolderPartialCompilationUnit(
            namespaceName,
            model.Holder.Name,
            tableClassName,
            stateType.FullyQualifiedDisplayString,
            triggerType.FullyQualifiedDisplayString);

        var hintPrefix = HintNameHelper.FromString(stateType.FullyQualifiedDisplayString);
        context.AddSource(
            $"{hintPrefix}.{tableClassName}.g.cs",
            SourceText.From(tableUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{model.Holder.Name}.g.cs",
            SourceText.From(holderUnit.ToFullString(), Encoding.UTF8));
    }

    private static void ReportIsolatedStates(
        SourceProductionContext context,
        EnumTypeInfo stateType,
        string initialMember,
        IReadOnlyCollection<ResolvedTransition> transitions,
        Location location)
    {
        var fromStates = new HashSet<string>(
            transitions.Select(static transition => transition.FromMember),
            StringComparer.Ordinal);

        foreach (var member in stateType.Members)
        {
            if (string.Equals(member.Name, initialMember, StringComparison.Ordinal))
            {
                continue;
            }

            if (!fromStates.Contains(member.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionIsolatedState,
                    location,
                    member.Name,
                    stateType.FullyQualifiedName));
            }
        }
    }

    private readonly record struct EnumMember(string Name, object? ConstantValue);

    private readonly record struct EnumTypeInfo(
        string Name,
        string? Namespace,
        string FullyQualifiedName,
        string FullyQualifiedDisplayString,
        EquatableArray<EnumMember> Members)
    {
        public static EnumTypeInfo FromSymbol(INamedTypeSymbol symbol)
        {
            var members = symbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(static field => field.IsConst)
                .Select(static field => new EnumMember(field.Name, field.ConstantValue))
                .ToArray();

            return new EnumTypeInfo(
                symbol.Name,
                symbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : symbol.ContainingNamespace.ToDisplayString(),
                symbol.ToDisplayString(),
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                new EquatableArray<EnumMember>(members));
        }
    }

    private readonly record struct InitialStateInfo(string? MemberName, string DisplayValue, bool IsValid);

    private readonly record struct TransitionArg(
        string? MemberName,
        string DisplayValue,
        bool IsValid);

    private sealed record TransitionModel(
        TransitionArg From,
        TransitionArg Trigger,
        TransitionArg To,
        Location Location);

    private sealed record StateMachineModel(
        ContractInfo Holder,
        EnumTypeInfo? StateType,
        EnumTypeInfo? TriggerType,
        InitialStateInfo Initial,
        EquatableArray<TransitionModel> Transitions,
        Location Location,
        bool IsValidHolder);

    private sealed class ResolvedTransition
    {
        public ResolvedTransition(
            string fromMember,
            string triggerMember,
            string toMember,
            Location location,
            string fromExpression,
            string triggerExpression,
            string toExpression)
        {
            FromMember = fromMember;
            TriggerMember = triggerMember;
            ToMember = toMember;
            Location = location;
            FromExpression = fromExpression;
            TriggerExpression = triggerExpression;
            ToExpression = toExpression;
        }

        public string FromMember { get; }

        public string TriggerMember { get; }

        public string ToMember { get; }

        public Location Location { get; }

        public string FromExpression { get; }

        public string TriggerExpression { get; }

        public string ToExpression { get; }
    }
}
