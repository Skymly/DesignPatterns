using System;
using System.Collections.Immutable;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports DP036 when <see cref="ITransitionTable{TState,TTrigger}.TryTransition"/>
/// (or <c>CanTransitionFrom</c>) is called with literal (state, trigger) arguments
/// that do not match any declared <c>[Transition]</c> edge.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StateTransitionLiteralEdgeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.StateTransitionInvalidLiteralEdge;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var edgesByTypePair = CollectTransitionEdges(context.Compilation);
        if (edgesByTypePair.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            syntaxContext => AnalyzeInvocation(syntaxContext, edgesByTypePair),
            SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// Scans all assemblies in the compilation for <c>[StateMachine]</c> holders
    /// and collects declared <c>[Transition]</c> edges keyed by (stateType, triggerType).
    /// </summary>
    private static ImmutableDictionary<TypePairKey, ImmutableHashSet<(int From, int Trigger)>> CollectTransitionEdges(
        Compilation compilation)
    {
        var builder = ImmutableDictionary.CreateBuilder<TypePairKey, ImmutableHashSet<(int From, int Trigger)>>();

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                INamedTypeSymbol? stateType = null;
                INamedTypeSymbol? triggerType = null;

                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    if (IsStateMachineAttribute(attribute.AttributeClass))
                    {
                        stateType = TryGetEnumTypeFromConstructorArg(attribute, 0);
                        triggerType = TryGetEnumTypeFromConstructorArg(attribute, 1);
                    }
                }

                if (stateType is null || triggerType is null)
                {
                    continue;
                }

                var key = new TypePairKey(stateType, triggerType);
                if (!builder.TryGetValue(key, out var edges))
                {
                    edges = ImmutableHashSet<(int, int)>.Empty;
                }

                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    if (!IsTransitionAttribute(attribute.AttributeClass))
                    {
                        continue;
                    }

                    if (attribute.ConstructorArguments.Length < 2)
                    {
                        continue;
                    }

                    var fromValue = TryGetEnumIntValue(attribute.ConstructorArguments[0]);
                    var triggerValue = TryGetEnumIntValue(attribute.ConstructorArguments[1]);
                    if (fromValue is null || triggerValue is null)
                    {
                        continue;
                    }

                    edges = edges.Add((fromValue.Value, triggerValue.Value));
                }

                if (edges.Count > 0)
                {
                    builder[key] = edges;
                }
            }
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableDictionary<TypePairKey, ImmutableHashSet<(int From, int Trigger)>> edgesByTypePair)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.Name is not ("TryTransition" or "CanTransitionFrom"))
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
        if (receiverType is null)
        {
            return;
        }

        if (!IsTransitionTable(receiverType, out var stateType, out var triggerType))
        {
            return;
        }

        var key = new TypePairKey(stateType, triggerType);
        if (!edgesByTypePair.TryGetValue(key, out var edges) || edges.IsEmpty)
        {
            return;
        }

        if (invocation.ArgumentList?.Arguments.Count is not > 0)
        {
            return;
        }

        // TryTransition(state, trigger, out next) — first two args are (state, trigger)
        // CanTransitionFrom(state) — only one arg (state); no trigger to validate
        if (method.Name == "CanTransitionFrom")
        {
            return;
        }

        if (invocation.ArgumentList!.Arguments.Count < 2)
        {
            return;
        }

        var stateExpr = invocation.ArgumentList.Arguments[0].Expression;
        var triggerExpr = invocation.ArgumentList.Arguments[1].Expression;

        var stateConstant = context.SemanticModel.GetConstantValue(stateExpr);
        var triggerConstant = context.SemanticModel.GetConstantValue(triggerExpr);

        if (!stateConstant.HasValue || !triggerConstant.HasValue)
        {
            return;
        }

        var stateInt = TryGetEnumIntFromConstantValue(stateConstant.Value);
        var triggerInt = TryGetEnumIntFromConstantValue(triggerConstant.Value);
        if (stateInt is null || triggerInt is null)
        {
            return;
        }

        if (edges.Contains((stateInt.Value, triggerInt.Value)))
        {
            return;
        }

        var stateName = TryGetEnumMemberName(stateType, stateInt.Value);
        var triggerName = TryGetEnumMemberName(triggerType, triggerInt.Value);

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            stateExpr.GetLocation(),
            stateName ?? stateInt.Value.ToString(),
            triggerName ?? triggerInt.Value.ToString(),
            stateType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            triggerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsStateMachineAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.MetadataName == "StateMachineAttribute" &&
               attributeClass.ContainingNamespace?.ToDisplayString() == "DesignPatterns.Behavioral";
    }

    private static bool IsTransitionAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null)
        {
            return false;
        }

        return attributeClass.MetadataName == "TransitionAttribute" &&
               attributeClass.ContainingNamespace?.ToDisplayString() == "DesignPatterns.Behavioral";
    }

    private static INamedTypeSymbol? TryGetEnumTypeFromConstructorArg(AttributeData attribute, int index)
    {
        if (attribute.ConstructorArguments.Length <= index)
        {
            return null;
        }

        var arg = attribute.ConstructorArguments[index];
        if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol typeSymbol)
        {
            return typeSymbol;
        }

        return null;
    }

    private static int? TryGetEnumIntValue(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return null;
        }

        if (constant.Value is null)
        {
            return null;
        }

        try
        {
            return System.Convert.ToInt32(constant.Value);
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetEnumIntFromConstantValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return System.Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTransitionTable(ITypeSymbol type, out INamedTypeSymbol stateType, out INamedTypeSymbol triggerType)
    {
        stateType = null!;
        triggerType = null!;

        // Check if the type itself is ITransitionTable<TState, TTrigger>
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.ToDisplayString() == StateTransitionAnalysisConstants.TransitionTableInterfaceDisplayName &&
            namedType.TypeArguments.Length == 2 &&
            namedType.TypeArguments[0] is INamedTypeSymbol stateDirect &&
            namedType.TypeArguments[1] is INamedTypeSymbol triggerDirect)
        {
            stateType = stateDirect;
            triggerType = triggerDirect;
            return true;
        }

        // Check if the type implements ITransitionTable<TState, TTrigger>
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() != StateTransitionAnalysisConstants.TransitionTableInterfaceDisplayName)
            {
                continue;
            }

            if (iface.TypeArguments.Length == 2 &&
                iface.TypeArguments[0] is INamedTypeSymbol state &&
                iface.TypeArguments[1] is INamedTypeSymbol trigger)
            {
                stateType = state;
                triggerType = trigger;
                return true;
            }
        }

        return false;
    }

    private static string? TryGetEnumMemberName(INamedTypeSymbol enumType, int value)
    {
        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol field || field.ConstantValue is null)
            {
                continue;
            }

            try
            {
                if (System.Convert.ToInt32(field.ConstantValue) == value)
                {
                    return field.Name;
                }
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private readonly struct TypePairKey : IEquatable<TypePairKey>
    {
        private readonly INamedTypeSymbol _stateType;
        private readonly INamedTypeSymbol _triggerType;
        private readonly int _hashCode;

        public TypePairKey(INamedTypeSymbol stateType, INamedTypeSymbol triggerType)
        {
            _stateType = stateType;
            _triggerType = triggerType;
            _hashCode = SymbolEqualityComparer.Default.GetHashCode(stateType) * 31 +
                        SymbolEqualityComparer.Default.GetHashCode(triggerType);
        }

        public bool Equals(TypePairKey other) =>
            SymbolEqualityComparer.Default.Equals(_stateType, other._stateType) &&
            SymbolEqualityComparer.Default.Equals(_triggerType, other._triggerType);

        public override bool Equals(object? obj) => obj is TypePairKey other && Equals(other);

        public override int GetHashCode() => _hashCode;
    }
}
