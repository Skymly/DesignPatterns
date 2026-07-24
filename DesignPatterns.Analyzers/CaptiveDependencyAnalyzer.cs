using System;
using System.Collections.Generic;
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
/// Reports DP062 when a Singleton service's constructor depends on a
/// Scoped or Transient service, creating a captive dependency, and
/// DP066 when a Singleton factory delegate resolves a Scoped or
/// Transient service. Covers MSDI registrations, generated RegisterDi
/// calls, and Autofac registrations (matched by symbol name, no
/// Autofac package reference).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaptiveDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.CaptiveDependency;

    private static readonly DiagnosticDescriptor FactoryDelegateRule =
        DesignPatternsDiagnosticDescriptors.FactoryDelegateCaptiveDependency;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule, FactoryDelegateRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var attributedTypes = AttributedRegistration.CollectByCategory(context.Compilation);
        var mapBuilder = new DiRegistrationMapBuilder(attributedTypes);

        context.RegisterSyntaxNodeAction(
            syntaxContext => CollectRegistration(syntaxContext, mapBuilder),
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(
            endContext => AnalyzeRegistrations(endContext, mapBuilder));
    }

    private static void CollectRegistration(
        SyntaxNodeAnalysisContext context,
        DiRegistrationMapBuilder mapBuilder)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;

        if (methodName is "AddSingleton" or "AddScoped" or "AddTransient" or "TryAdd"
            or "RegisterType" or "Register" or "RegisterDi")
        {
            mapBuilder.TryCollect(invocation, context.SemanticModel);
        }
    }

    private static void AnalyzeRegistrations(
        CompilationAnalysisContext context,
        DiRegistrationMapBuilder mapBuilder)
    {
        var map = mapBuilder.Build();
        if (map.Entries.Count == 0 && map.FactoryDelegates.Count == 0)
        {
            return;
        }

        // Build the registration map: type → lifetime (last wins).
        var lifetimeMap = new Dictionary<INamedTypeSymbol, Lifetime>(
            SymbolEqualityComparer.Default);

        foreach (var pair in map.Lifetimes)
        {
            lifetimeMap[pair.Key] = pair.Value;
        }

        // DP062: Singleton constructor analysis for all map entries
        // (explicit container registrations and attributed RegisterDi).
        foreach (var reg in map.Entries)
        {
            if (reg.Lifetime != Lifetime.Singleton || reg.SkipConstructorAnalysis)
            {
                continue;
            }

            AnalyzeSingleton(context, reg.ImplementationType, reg.Invocation, lifetimeMap);
        }

        // DP066: Singleton factory delegates collected on the map.
        foreach (var factory in map.FactoryDelegates)
        {
            AnalyzeFactoryDelegate(context, factory, lifetimeMap);
        }
    }

    /// <summary>
    /// Scans a Singleton factory delegate body for direct
    /// <c>GetRequiredService&lt;X&gt;</c> / <c>GetService&lt;X&gt;</c> (MSDI) or
    /// <c>Resolve&lt;X&gt;</c> (Autofac) calls and reports DP066 when the
    /// resolved service is registered as Scoped or Transient. Indirect
    /// resolutions (helper methods, method groups) are not detected —
    /// documented limitation.
    /// </summary>
    private static void AnalyzeFactoryDelegate(
        CompilationAnalysisContext context,
        FactoryDelegateRegistration factory,
        Dictionary<INamedTypeSymbol, Lifetime> lifetimeMap)
    {
        var semanticModel = factory.SemanticModel;

        foreach (var invocation in factory.Lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var name = memberAccess.Name.Identifier.ValueText;
            if (name is not ("GetRequiredService" or "GetService" or "Resolve"))
            {
                continue;
            }

            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol ||
                !IsServiceResolutionMethod(methodSymbol))
            {
                continue;
            }

            if (methodSymbol.TypeArguments.Length != 1 ||
                methodSymbol.TypeArguments[0] is not INamedTypeSymbol resolvedType)
            {
                continue;
            }

            if (lifetimeMap.TryGetValue(resolvedType, out var resolvedLifetime) &&
                resolvedLifetime is Lifetime.Scoped or Lifetime.Transient)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    FactoryDelegateRule,
                    invocation.GetLocation(),
                    factory.ServiceType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    resolvedLifetime.ToString(),
                    resolvedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }

    private static bool IsServiceResolutionMethod(IMethodSymbol methodSymbol)
    {
        var ns = methodSymbol.ContainingNamespace?.ToDisplayString();
        return ns is not null &&
               (ns.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) ||
                ns.StartsWith("Autofac", StringComparison.Ordinal));
    }

    private static void AnalyzeSingleton(
        CompilationAnalysisContext context,
        INamedTypeSymbol implType,
        InvocationExpressionSyntax invocation,
        Dictionary<INamedTypeSymbol, Lifetime> lifetimeMap)
    {
        // Skip if not a class or struct (e.g. interface, delegate)
        if (implType.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        // Find the first public instance constructor.
        var constructors = implType.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        if (constructors.Count == 0)
        {
            return;
        }

        // Pick the first constructor (documented limitation:
        // ActivatorUtilities picks the longest, we pick the first).
        var ctor = constructors[0];

        foreach (var param in ctor.Parameters)
        {
            if (param.Type is not INamedTypeSymbol paramType)
            {
                continue;
            }

            if (lifetimeMap.TryGetValue(paramType, out var paramLifetime) &&
                paramLifetime is Lifetime.Scoped or Lifetime.Transient)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    invocation.GetLocation(),
                    implType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    paramLifetime.ToString(),
                    paramType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }
}
