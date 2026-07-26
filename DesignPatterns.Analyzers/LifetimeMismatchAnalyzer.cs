using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Analyzers.Di;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports DI lifetime mismatches in generated <c>RegisterDi</c> calls.
/// <para>
/// DP060 (Warning): captive dependency — <c>registryLifetime</c> exceeds <c>implementationLifetime</c>.
/// DP061 (Info): wasteful — <c>implementationLifetime</c> exceeds <c>registryLifetime</c>.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LifetimeMismatchAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CaptiveRule =
        DesignPatternsDiagnosticDescriptors.DiLifetimeCaptiveDependency;

    private static readonly DiagnosticDescriptor WastefulRule =
        DesignPatternsDiagnosticDescriptors.DiLifetimeWasteful;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CaptiveRule, WastefulRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (memberAccess.Name.Identifier.ValueText != "RegisterDi")
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (methodSymbol is null || !methodSymbol.IsStatic || methodSymbol.Parameters.Length == 0)
        {
            return;
        }

        // The first parameter must be IServiceCollection.
        var firstParamType = methodSymbol.Parameters[0].Type;
        if (firstParamType.ToDisplayString() != "Microsoft.Extensions.DependencyInjection.IServiceCollection")
        {
            return;
        }

        // Find lifetime parameters by name.
        var implementationLifetimeParam = methodSymbol.Parameters.FirstOrDefault(
            p => p.Name == "implementationLifetime");
        var registryLifetimeParam = methodSymbol.Parameters.FirstOrDefault(
            p => p.Name == "registryLifetime");

        // Only the two-lifetime overload can have a mismatch.
        // The single-lifetime overload (e.g. StateTransition RegisterDi) is skipped.
        if (implementationLifetimeParam is null || registryLifetimeParam is null)
        {
            return;
        }

        var implementationLifetime = LifetimeResolution.TryResolveArgument(
            invocation, implementationLifetimeParam, context.SemanticModel);
        var registryLifetime = LifetimeResolution.TryResolveArgument(
            invocation, registryLifetimeParam, context.SemanticModel);

        // If either lifetime cannot be resolved (e.g. user passed a variable), use the default.
        implementationLifetime ??= GetDefaultLifetime(methodSymbol);
        registryLifetime ??= Lifetime.Singleton;

        if (IsLonger(registryLifetime.Value, implementationLifetime.Value))
        {
            // Captive dependency: registry outlives implementations.
            var containingType = methodSymbol.ContainingType?.ToDisplayString(
                SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "registry";

            context.ReportDiagnostic(Diagnostic.Create(
                CaptiveRule,
                invocation.GetLocation(),
                containingType,
                LifetimeName(registryLifetime.Value),
                LifetimeName(implementationLifetime.Value)));
        }
        else if (IsLonger(implementationLifetime.Value, registryLifetime.Value))
        {
            // Wasteful: implementations outlive registry.
            var containingType = methodSymbol.ContainingType?.ToDisplayString(
                SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "registry";

            context.ReportDiagnostic(Diagnostic.Create(
                WastefulRule,
                invocation.GetLocation(),
                containingType,
                LifetimeName(implementationLifetime.Value),
                LifetimeName(registryLifetime.Value)));
        }
    }

    private static Lifetime GetDefaultLifetime(IMethodSymbol methodSymbol)
    {
        // Factory RegisterDi defaults to Transient; others default to Singleton.
        var containingTypeName = methodSymbol.ContainingType?.Name ?? "";
        return containingTypeName.Contains("Factory") ? Lifetime.Transient : Lifetime.Singleton;
    }

    private static bool IsLonger(Lifetime a, Lifetime b) =>
        LifetimeRank(a) > LifetimeRank(b);

    private static int LifetimeRank(Lifetime lifetime) =>
        lifetime switch
        {
            Lifetime.Singleton => 3,
            Lifetime.Scoped => 2,
            Lifetime.Transient => 1,
            _ => 0,
        };

    private static string LifetimeName(Lifetime lifetime) =>
        lifetime switch
        {
            Lifetime.Singleton => "Singleton",
            Lifetime.Scoped => "Scoped",
            Lifetime.Transient => "Transient",
            _ => lifetime.ToString(),
        };
}
