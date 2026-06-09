using System.Collections.Immutable;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports compile-time string keys that are not registered for a generator-managed strategy or factory contract.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnknownRegistryKeyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.RegistryKeyNotRegistered;

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
        var registeredKeysByContract = AnalyzerSymbolHelper.CollectRegisteredKeysByContract(context.Compilation);
        if (registeredKeysByContract.IsEmpty)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            syntaxContext => AnalyzeInvocation(syntaxContext, registeredKeysByContract),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableDictionary<INamedTypeSymbol, ImmutableHashSet<string>> registeredKeysByContract)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
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

        if (!AnalyzerSymbolHelper.IsRegistryKeyInvocation(receiverType, method))
        {
            return;
        }

        if (!AnalyzerSymbolHelper.TryGetRegistryContract(receiverType, out var contractType))
        {
            return;
        }

        if (!registeredKeysByContract.TryGetValue(contractType, out var registeredKeys) || registeredKeys.IsEmpty)
        {
            return;
        }

        if (invocation.ArgumentList?.Arguments.Count is not > 0)
        {
            return;
        }

        var keyExpression = invocation.ArgumentList.Arguments[0].Expression;
        var constantValue = context.SemanticModel.GetConstantValue(keyExpression);
        if (!constantValue.HasValue || constantValue.Value is not string keyValue)
        {
            return;
        }

        if (string.IsNullOrEmpty(keyValue) || registeredKeys.Contains(keyValue))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty;
        var suggestedKey = AnalyzerSymbolHelper.FindClosestRegistryKey(keyValue, registeredKeys, maxDistance: 2);
        if (suggestedKey is not null)
        {
            properties = properties.Add(AnalyzerSymbolHelper.SuggestedRegistryKeyPropertyName, suggestedKey);
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            keyExpression.GetLocation(),
            properties,
            keyValue,
            contractType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            AnalyzerSymbolHelper.FormatRegisteredKeysForMessage(registeredKeys)));
    }
}
