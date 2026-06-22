using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Generators.StateTransition;

/// <summary>
/// Extraction stage: parses <c>[StateMachine]</c> and <c>[Transition]</c>
/// attributes into a <see cref="StateMachineModel"/>.
/// </summary>
internal static class StateTransitionTransform
{
    /// <summary>
    /// Extracts a <see cref="StateMachineModel"/> from the attribute context,
    /// or returns <see cref="Result{T}.Empty"/> when the target is not a
    /// valid state machine holder.
    /// </summary>
    public static Result<StateMachineModel> Transform(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol holderType)
        {
            return Result<StateMachineModel>.Empty;
        }

        var stateMachineAttribute = context.Attributes.FirstOrDefault(attribute =>
            attribute.AttributeClass is not null &&
            string.Equals(
                attribute.AttributeClass.Name,
                "StateMachineAttribute",
                StringComparison.Ordinal));

        if (stateMachineAttribute is null)
        {
            return Result<StateMachineModel>.Empty;
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

            string? guardName = null;
            foreach (var namedArgument in attribute.NamedArguments)
            {
                if (string.Equals(namedArgument.Key, "Guard", StringComparison.Ordinal)
                    && namedArgument.Value.Value is string guardValue)
                {
                    guardName = guardValue;
                }
            }

            var guard = guardName is null || stateType is null || triggerType is null
                ? default(GuardResolution)
                : ResolveGuard(holderType, guardName, stateType, triggerType);

            transitions.Add(new TransitionModel(from, trigger, to, transitionLocation, guard));
        }

        return Result<StateMachineModel>.Success(new StateMachineModel(
            holderInfo,
            stateTypeInfo,
            triggerTypeInfo,
            initialState,
            new EquatableArray<TransitionModel>(transitions.ToArray()),
            location,
            isValidHolder));
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

    private static GuardResolution ResolveGuard(
        INamedTypeSymbol holderType,
        string guardName,
        INamedTypeSymbol stateType,
        INamedTypeSymbol triggerType)
    {
        var methods = holderType.GetMembers(guardName)
            .OfType<IMethodSymbol>()
            .ToList();

        if (methods.Count == 0)
        {
            return new GuardResolution(guardName, IsFound: false, IsStatic: false, HasValidSignature: false, MethodReference: null);
        }

        var method = methods[0];
        var holderFqn = holderType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var methodReference = $"{holderFqn}.{method.Name}";

        if (!method.IsStatic)
        {
            return new GuardResolution(guardName, IsFound: true, IsStatic: false, HasValidSignature: false, MethodReference: null);
        }

        var hasValidSignature = method.ReturnType.SpecialType == SpecialType.System_Boolean
            && method.Parameters.Length == 2
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType)
            && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, triggerType);

        return new GuardResolution(
            guardName,
            IsFound: true,
            IsStatic: true,
            HasValidSignature: hasValidSignature,
            MethodReference: hasValidSignature ? methodReference : null);
    }
}
