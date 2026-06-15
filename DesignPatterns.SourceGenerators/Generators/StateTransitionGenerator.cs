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
            transitions.Add(new TransitionModel(
                attribute.ConstructorArguments[0],
                attribute.ConstructorArguments[1],
                attribute.ConstructorArguments[2],
                transitionLocation));
        }

        return new StateMachineModel(
            holderType,
            stateType,
            triggerType,
            initial,
            transitions,
            location,
            isValidHolder);
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
                model.HolderType.Name));
            return;
        }

        if (model.StateType is null
            || model.TriggerType is null
            || model.StateType.TypeKind != TypeKind.Enum
            || model.TriggerType.TypeKind != TypeKind.Enum)
        {
            return;
        }

        var stateType = model.StateType;
        var triggerType = model.TriggerType;
        var stateTypeName = stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var triggerTypeName = triggerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (!TryResolveEnumMember(model.Initial, stateType, out var initialMember, out var initialDisplay))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DesignPatternsDiagnosticDescriptors.StateTransitionInvalidInitialState,
                model.Location,
                initialDisplay,
                stateType.ToDisplayString()));
            return;
        }

        var validTransitions = new List<ResolvedTransition>();
        foreach (var transition in model.Transitions)
        {
            if (!TryResolveEnumMember(transition.From, stateType, out var fromMember, out var fromDisplay))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidStateMember,
                    transition.Location,
                    fromDisplay,
                    stateType.ToDisplayString()));
                continue;
            }

            if (!TryResolveEnumMember(transition.To, stateType, out var toMember, out var toDisplay))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidStateMember,
                    transition.Location,
                    toDisplay,
                    stateType.ToDisplayString()));
                continue;
            }

            if (!TryResolveEnumMember(transition.Trigger, triggerType, out var triggerMember, out var triggerDisplay))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionInvalidTriggerMember,
                    transition.Location,
                    triggerDisplay,
                    triggerType.ToDisplayString()));
                continue;
            }

            validTransitions.Add(new ResolvedTransition(
                fromMember,
                triggerMember,
                toMember,
                transition.Location,
                StateTransitionSyntaxFactory.FormatEnumMember(stateType, fromMember),
                StateTransitionSyntaxFactory.FormatEnumMember(triggerType, triggerMember),
                StateTransitionSyntaxFactory.FormatEnumMember(stateType, toMember)));
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

        var namespaceName = model.HolderType.ContainingNamespace.IsGlobalNamespace
            ? null
            : model.HolderType.ContainingNamespace.ToDisplayString();

        var tableClassName = StateTransitionSyntaxFactory.GetTransitionTableClassName(stateType);
        var initialExpression = StateTransitionSyntaxFactory.FormatEnumMember(stateType, initialMember);
        var transitionExpressions = uniqueTransitions
            .Select(static transition => (transition.FromExpression, transition.TriggerExpression, transition.ToExpression))
            .ToList();

        var tableUnit = StateTransitionSyntaxFactory.CreateTransitionTableCompilationUnit(
            namespaceName,
            tableClassName,
            stateTypeName,
            triggerTypeName,
            initialExpression,
            transitionExpressions);

        var holderUnit = StateTransitionSyntaxFactory.CreateHolderPartialCompilationUnit(
            namespaceName,
            model.HolderType.Name,
            tableClassName,
            stateTypeName,
            triggerTypeName);

        var hintPrefix = HintNameHelper.FromSymbol(stateType);
        context.AddSource(
            $"{hintPrefix}.{tableClassName}.g.cs",
            SourceText.From(tableUnit.ToFullString(), Encoding.UTF8));

        context.AddSource(
            $"{hintPrefix}.{model.HolderType.Name}.g.cs",
            SourceText.From(holderUnit.ToFullString(), Encoding.UTF8));
    }

    private static void ReportIsolatedStates(
        SourceProductionContext context,
        INamedTypeSymbol stateType,
        string initialMember,
        IReadOnlyCollection<ResolvedTransition> transitions,
        Location location)
    {
        var fromStates = new HashSet<string>(
            transitions.Select(static transition => transition.FromMember),
            StringComparer.Ordinal);

        foreach (var field in stateType.GetMembers().OfType<IFieldSymbol>().Where(static field => field.IsConst))
        {
            if (string.Equals(field.Name, initialMember, StringComparison.Ordinal))
            {
                continue;
            }

            if (!fromStates.Contains(field.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignPatternsDiagnosticDescriptors.StateTransitionIsolatedState,
                    location,
                    field.Name,
                    stateType.ToDisplayString()));
            }
        }
    }

    private static bool TryResolveEnumMember(
        TypedConstant? constant,
        INamedTypeSymbol enumType,
        out string memberName,
        out string displayValue)
    {
        memberName = string.Empty;
        displayValue = string.Empty;

        if (constant is null)
        {
            displayValue = "<missing>";
            return false;
        }

        var typedConstant = constant.Value;
        if (typedConstant.Kind != TypedConstantKind.Error
            && typedConstant.Value is null)
        {
            displayValue = "<missing>";
            return false;
        }

        if (typedConstant.Kind != TypedConstantKind.Enum
            || typedConstant.Type is not INamedTypeSymbol constantEnumType
            || !SymbolEqualityComparer.Default.Equals(constantEnumType, enumType))
        {
            displayValue = typedConstant.ToCSharpString();
            return false;
        }

        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>().Where(static field => field.IsConst))
        {
            if (Equals(field.ConstantValue, typedConstant.Value))
            {
                memberName = field.Name;
                displayValue = field.Name;
                return true;
            }
        }

        displayValue = typedConstant.ToCSharpString();
        return false;
    }

    private sealed class StateMachineModel
    {
        public StateMachineModel(
            INamedTypeSymbol holderType,
            INamedTypeSymbol? stateType,
            INamedTypeSymbol? triggerType,
            TypedConstant? initial,
            IReadOnlyList<TransitionModel> transitions,
            Location location,
            bool isValidHolder)
        {
            HolderType = holderType;
            StateType = stateType;
            TriggerType = triggerType;
            Initial = initial;
            Transitions = transitions;
            Location = location;
            IsValidHolder = isValidHolder;
        }

        public INamedTypeSymbol HolderType { get; }

        public INamedTypeSymbol? StateType { get; }

        public INamedTypeSymbol? TriggerType { get; }

        public TypedConstant? Initial { get; }

        public IReadOnlyList<TransitionModel> Transitions { get; }

        public Location Location { get; }

        public bool IsValidHolder { get; }
    }

    private sealed class TransitionModel
    {
        public TransitionModel(
            TypedConstant from,
            TypedConstant trigger,
            TypedConstant to,
            Location location)
        {
            From = from;
            Trigger = trigger;
            To = to;
            Location = location;
        }

        public TypedConstant From { get; }

        public TypedConstant Trigger { get; }

        public TypedConstant To { get; }

        public Location Location { get; }
    }

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
