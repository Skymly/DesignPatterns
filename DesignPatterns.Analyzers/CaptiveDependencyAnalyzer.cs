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

    /// <summary>
    /// Categories of DesignPatterns registration attributes.
    /// Used to match attributed types to the correct RegisterDi call
    /// based on the containing type name pattern.
    /// </summary>
    private enum RegistrationCategory
    {
        Strategy,
        Factory,
        EventHandler,
        Decorator,
        Composite,
    }

    private sealed class RegistrationEntry
    {
        public INamedTypeSymbol ImplementationType { get; set; } = null!;
        public Lifetime Lifetime { get; set; }
        public InvocationExpressionSyntax Invocation { get; set; } = null!;
    }

    /// <summary>
    /// A Singleton factory delegate whose body is analyzed at compilation end
    /// for resolutions of shorter-lived services (DP066).
    /// </summary>
    private sealed class FactoryDelegateEntry
    {
        public INamedTypeSymbol ServiceType { get; set; } = null!;
        public AnonymousFunctionExpressionSyntax Lambda { get; set; } = null!;

        /// <summary>Semantic model of the lambda's tree, captured at collection time.</summary>
        public SemanticModel SemanticModel { get; set; } = null!;
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var mapBuilder = new DiRegistrationMapBuilder();
        var registerDiEntries = new List<RegistrationEntry>();
        var factoryDelegates = new List<FactoryDelegateEntry>();

        // Phase 2: Pre-scan all types for DesignPatterns registration attributes.
        // Types are grouped by category so each RegisterDi call only applies
        // to the implementation types of its own pattern.
        var attributedTypesByCategory = CollectAttributedImplementationTypes(context.Compilation);

        context.RegisterSyntaxNodeAction(
            syntaxContext => CollectRegistration(
                syntaxContext,
                mapBuilder,
                registerDiEntries,
                factoryDelegates,
                attributedTypesByCategory),
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(
            endContext => AnalyzeRegistrations(endContext, mapBuilder, registerDiEntries, factoryDelegates));
    }

    /// <summary>
    /// Scans all types in the compilation for DesignPatterns registration attributes
    /// and groups them by category for matching to the correct RegisterDi call.
    /// </summary>
    private static Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> CollectAttributedImplementationTypes(
        Compilation compilation)
    {
        var result = new Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>>
        {
            [RegistrationCategory.Strategy] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.Factory] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.EventHandler] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.Decorator] = new(SymbolEqualityComparer.Default),
            [RegistrationCategory.Composite] = new(SymbolEqualityComparer.Default),
        };

        foreach (var assembly in AnalyzerSymbolHelper.GetAssembliesInCompilation(compilation))
        {
            foreach (var typeSymbol in AnalyzerSymbolHelper.GetAllTypes(assembly.GlobalNamespace))
            {
                foreach (var attribute in typeSymbol.GetAttributes())
                {
                    var attrName = attribute.AttributeClass?.ToDisplayString();
                    if (attrName is null)
                    {
                        continue;
                    }

                    var category = attrName switch
                    {
                        _ when attrName == StrategyAnalysisConstants.RegisterStrategyMetadataName =>
                            RegistrationCategory.Strategy,
                        _ when attrName == FactoryAnalysisConstants.RegisterFactoryMetadataName =>
                            RegistrationCategory.Factory,
                        _ when attrName == EventHandlerAnalysisConstants.RegisterEventHandlerMetadataName =>
                            RegistrationCategory.EventHandler,
                        "DesignPatterns.Structural.DecoratorAttribute" =>
                            RegistrationCategory.Decorator,
                        "DesignPatterns.Structural.CompositePartAttribute" =>
                            RegistrationCategory.Composite,
                        _ => (RegistrationCategory?)null,
                    };

                    if (category is { } cat)
                    {
                        result[cat].Add(typeSymbol);
                        break;
                    }
                }
            }
        }

        return result;
    }

    private static void CollectRegistration(
        SyntaxNodeAnalysisContext context,
        DiRegistrationMapBuilder mapBuilder,
        List<RegistrationEntry> registerDiEntries,
        List<FactoryDelegateEntry> factoryDelegates,
        Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> attributedTypesByCategory)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;

        if (methodName is "AddSingleton" or "AddScoped" or "AddTransient" or "TryAdd" or "RegisterType" or "Register")
        {
            if (mapBuilder.TryCollect(invocation, context.SemanticModel))
            {
                TryCollectFactoryDelegate(context, invocation, methodName, factoryDelegates);
            }
        }
        else if (methodName == "RegisterDi")
        {
            CollectRegisterDiRegistration(context, invocation, attributedTypesByCategory, registerDiEntries);
        }
    }

    /// <summary>
    /// Collects Singleton factory delegates for DP066. Lifetime map entries for
    /// these registrations are owned by <see cref="DiRegistrationMapBuilder"/>.
    /// </summary>
    private static void TryCollectFactoryDelegate(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string methodName,
        List<FactoryDelegateEntry> factoryDelegates)
    {
        var args = invocation.ArgumentList?.Arguments ?? default;
        if (args.Count == 0 || args[0].Expression is not AnonymousFunctionExpressionSyntax lambda)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (methodSymbol is null)
        {
            return;
        }

        if (methodName == "AddSingleton")
        {
            if (methodSymbol.TypeArguments.Length != 1 ||
                methodSymbol.TypeArguments[0] is not INamedTypeSymbol serviceType ||
                AutofacRegistration.IsOpenGeneric(serviceType))
            {
                return;
            }

            factoryDelegates.Add(new FactoryDelegateEntry
            {
                ServiceType = serviceType,
                Lambda = lambda,
                SemanticModel = context.SemanticModel,
            });
            return;
        }

        if (methodName == "Register" && AutofacRegistration.IsAutofacMethod(methodSymbol))
        {
            if (methodSymbol.TypeArguments.Length != 1 ||
                methodSymbol.TypeArguments[0] is not INamedTypeSymbol serviceType ||
                AutofacRegistration.IsOpenGeneric(serviceType))
            {
                return;
            }

            if (AutofacRegistration.ResolveChainLifetime(invocation) != Lifetime.Singleton)
            {
                return;
            }

            factoryDelegates.Add(new FactoryDelegateEntry
            {
                ServiceType = serviceType,
                Lambda = lambda,
                SemanticModel = context.SemanticModel,
            });
        }
    }

    /// <summary>
    /// Collects registrations from generated <c>RegisterDi</c> calls.
    /// Extracts the implementation lifetime and applies it to the types
    /// bearing the DesignPatterns registration attribute that matches
    /// the RegisterDi holder's pattern (Strategy/Factory/EventHandler/Decorator/Composite).
    /// </summary>
    private static void CollectRegisterDiRegistration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> attributedTypesByCategory,
        List<RegistrationEntry> registrations)
    {
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

        // Find the implementation lifetime parameter by name.
        // Two-lifetime overload: implementationLifetime + registryLifetime
        // Single-lifetime overload: lifetime (StateTransition, Composite, Decorator, EventHandler)
        var implLifetimeParam = methodSymbol.Parameters.FirstOrDefault(
            p => p.Name == "implementationLifetime");
        if (implLifetimeParam is null)
        {
            implLifetimeParam = methodSymbol.Parameters.FirstOrDefault(
                p => p.Name == "lifetime");
        }

        if (implLifetimeParam is null)
        {
            return;
        }

        // Resolve the lifetime value from the call arguments.
        var lifetime = LifetimeResolution.TryResolveArgument(
            invocation, implLifetimeParam, context.SemanticModel);
        var containingTypeName = methodSymbol.ContainingType?.Name ?? "";
        if (lifetime is null)
        {
            // Use default: Factory → Transient, others → Singleton.
            lifetime = containingTypeName.Contains("Factory") ? Lifetime.Transient : Lifetime.Singleton;
        }

        // Match the RegisterDi holder type name to the correct attribute category.
        // This prevents cross-pattern contamination (e.g. a Strategy RegisterDi call
        // should not apply its lifetime to Factory implementation types).
        var category = MatchCategoryByHolderName(containingTypeName);
        if (category is null)
        {
            return;
        }

        if (!attributedTypesByCategory.TryGetValue(category.Value, out var typesForCategory))
        {
            return;
        }

        // Add only the implementation types of the matched category.
        foreach (var implType in typesForCategory)
        {
            // Skip open generics
            if (implType.IsUnboundGenericType ||
                (implType.TypeParameters.Length > 0 && implType.IsDefinition))
            {
                continue;
            }

            registrations.Add(new RegistrationEntry
            {
                ImplementationType = implType,
                Lifetime = lifetime.Value,
                Invocation = invocation,
            });
        }
    }

    /// <summary>
    /// Maps a RegisterDi holder type name to its registration category
    /// based on naming conventions used by the source generators.
    /// </summary>
    private static RegistrationCategory? MatchCategoryByHolderName(string holderTypeName)
    {
        if (holderTypeName.Contains("Strategy"))
        {
            return RegistrationCategory.Strategy;
        }

        if (holderTypeName.Contains("Factory"))
        {
            return RegistrationCategory.Factory;
        }

        if (holderTypeName.Contains("EventHandler") || holderTypeName.Contains("EventAggregator"))
        {
            return RegistrationCategory.EventHandler;
        }

        if (holderTypeName.Contains("Decorator"))
        {
            return RegistrationCategory.Decorator;
        }

        if (holderTypeName.Contains("Composite"))
        {
            return RegistrationCategory.Composite;
        }

        return null;
    }

    private static void AnalyzeRegistrations(
        CompilationAnalysisContext context,
        DiRegistrationMapBuilder mapBuilder,
        List<RegistrationEntry> registerDiEntries,
        List<FactoryDelegateEntry> factoryDelegates)
    {
        var map = mapBuilder.Build();
        if (map.Entries.Count == 0 && registerDiEntries.Count == 0)
        {
            return;
        }

        // Build the registration map: type → lifetime (last wins).
        // Explicit container registrations come from DiRegistrationMap;
        // attributed RegisterDi entries are overlaid (still private until #234).
        var lifetimeMap = new Dictionary<INamedTypeSymbol, Lifetime>(
            SymbolEqualityComparer.Default);

        foreach (var pair in map.Lifetimes)
        {
            lifetimeMap[pair.Key] = pair.Value;
        }

        foreach (var reg in registerDiEntries)
        {
            lifetimeMap[reg.ImplementationType] = reg.Lifetime;
        }

        // DP062: Singleton constructor analysis for explicit registrations from the map.
        foreach (var reg in map.Entries)
        {
            if (reg.Lifetime != Lifetime.Singleton || reg.SkipConstructorAnalysis)
            {
                continue;
            }

            AnalyzeSingleton(context, reg.ImplementationType, reg.Invocation, lifetimeMap);
        }

        // DP062: RegisterDi path still uses the private entry list.
        foreach (var reg in registerDiEntries)
        {
            if (reg.Lifetime != Lifetime.Singleton)
            {
                continue;
            }

            AnalyzeSingleton(context, reg.ImplementationType, reg.Invocation, lifetimeMap);
        }

        // For each Singleton factory delegate, check resolved services (DP066).
        foreach (var factory in factoryDelegates)
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
        FactoryDelegateEntry factory,
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
