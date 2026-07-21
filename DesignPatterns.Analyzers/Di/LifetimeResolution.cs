using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// Shared primitives for resolving DI lifetime arguments from Roslyn syntax.
/// </summary>
internal static class LifetimeResolution
{
    /// <summary>
    /// Resolves a <see cref="Lifetime"/> from an expression that is a compile-time
    /// constant matching MSDI <c>ServiceLifetime</c> values (0/1/2) or a constant field.
    /// Returns <see langword="null"/> when the value cannot be determined.
    /// </summary>
    internal static Lifetime? TryResolve(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is int intValue)
        {
            return FromInt(intValue);
        }

        var symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IFieldSymbol field && field.HasConstantValue)
        {
            return field.ConstantValue is int fieldValue ? FromInt(fieldValue) : null;
        }

        return null;
    }

    /// <summary>
    /// Resolves a lifetime argument on an invocation by matching the given parameter
    /// (named argument first, then positional). Returns <see langword="null"/> when
    /// the argument is omitted or its value cannot be resolved.
    /// </summary>
    internal static Lifetime? TryResolveArgument(
        InvocationExpressionSyntax invocation,
        IParameterSymbol parameter,
        SemanticModel semanticModel)
    {
        var argument = FindArgument(invocation.ArgumentList, parameter);
        if (argument is null)
        {
            return null;
        }

        return TryResolve(argument.Expression, semanticModel);
    }

    private static ArgumentSyntax? FindArgument(
        ArgumentListSyntax? argumentList,
        IParameterSymbol parameter)
    {
        if (argumentList is null)
        {
            return null;
        }

        foreach (var arg in argumentList.Arguments)
        {
            if (arg.NameColon is { Name.Identifier.ValueText: var name } &&
                name == parameter.Name)
            {
                return arg;
            }
        }

        var index = parameter.Ordinal;
        if (index < argumentList.Arguments.Count)
        {
            var arg = argumentList.Arguments[index];
            if (arg.NameColon is null)
            {
                return arg;
            }
        }

        return null;
    }

    private static Lifetime? FromInt(int value) =>
        value switch
        {
            0 => Lifetime.Singleton,
            1 => Lifetime.Scoped,
            2 => Lifetime.Transient,
            _ => null,
        };
}
