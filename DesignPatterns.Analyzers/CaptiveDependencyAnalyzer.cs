using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DesignPatterns.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DesignPatterns.Analyzers;

/// <summary>
/// Reports DP062 when a Singleton service's constructor depends on a
/// Scoped or Transient service, creating a captive dependency.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaptiveDependencyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        DesignPatternsDiagnosticDescriptors.CaptiveDependency;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private enum Lifetime
    {
        Singleton = 0,
        Scoped = 1,
        Transient = 2,
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

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var registrations = new List<RegistrationEntry>();

        // Phase 2: Pre-scan all types for DesignPatterns registration attributes.
        // Types are grouped by category so each RegisterDi call only applies
        // to the implementation types of its own pattern.
        var attributedTypesByCategory = CollectAttributedImplementationTypes(context.Compilation);

        context.RegisterSyntaxNodeAction(
            syntaxContext => CollectRegistration(syntaxContext, registrations, attributedTypesByCategory),
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(
            endContext => AnalyzeRegistrations(endContext, registrations));
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
        List<RegistrationEntry> registrations,
        Dictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> attributedTypesByCategory)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;

        if (methodName is "AddSingleton" or "AddScoped" or "AddTransient")
        {
            CollectDirectRegistration(context, invocation, methodName, registrations);
        }
        else if (methodName == "TryAdd")
        {
            CollectTryAddRegistration(context, invocation, registrations);
        }
        else if (methodName == "RegisterDi")
        {
            CollectRegisterDiRegistration(context, invocation, attributedTypesByCategory, registrations);
        }
    }

    private static void CollectDirectRegistration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string methodName,
        List<RegistrationEntry> registrations)
    {
        var lifetime = methodName switch
        {
            "AddSingleton" => Lifetime.Singleton,
            "AddScoped" => Lifetime.Scoped,
            "AddTransient" => Lifetime.Transient,
            _ => Lifetime.Transient,
        };

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (methodSymbol is null)
        {
            return;
        }

        INamedTypeSymbol? implType = null;
        var args = invocation.ArgumentList?.Arguments ?? new SeparatedSyntaxList<ArgumentSyntax>();

        // AddSingleton<TService, TImpl>() — 2 type args, TImpl is implementation
        if (methodSymbol.TypeArguments.Length == 2 &&
            methodSymbol.TypeArguments[1] is INamedTypeSymbol implFromGeneric)
        {
            implType = implFromGeneric;
        }
        // AddSingleton<TImpl>() — 1 type arg, TImpl is implementation
        else if (methodSymbol.TypeArguments.Length == 1 &&
                 methodSymbol.TypeArguments[0] is INamedTypeSymbol singleGeneric)
        {
            // Skip if the single arg has a lambda or instance argument (factory/instance registration)
            if (args.Count > 0)
            {
                if (IsFactoryOrInstanceArg(args[0], context.SemanticModel))
                {
                    return;
                }
            }

            implType = singleGeneric;
        }

        if (implType is null)
        {
            return;
        }

        // Skip open generics
        if (implType.IsUnboundGenericType ||
            (implType.TypeParameters.Length > 0 && implType.IsDefinition))
        {
            return;
        }

        registrations.Add(new RegistrationEntry
        {
            ImplementationType = implType,
            Lifetime = lifetime,
            Invocation = invocation,
        });
    }

    private static void CollectTryAddRegistration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        List<RegistrationEntry> registrations)
    {
        var argList = invocation.ArgumentList;
        if (argList is null || argList.Arguments.Count == 0)
        {
            return;
        }

        // TryAdd(new ServiceDescriptor(typeof(TService), typeof(TImpl), ServiceLifetime.Singleton))
        var firstArg = argList.Arguments[0].Expression;
        if (firstArg is not ObjectCreationExpressionSyntax objectCreation)
        {
            return;
        }

        var descriptorArgList = objectCreation.ArgumentList;
        if (descriptorArgList is null || descriptorArgList.Arguments.Count < 3)
        {
            return;
        }

        var descriptorArgs = descriptorArgList.Arguments;

        // Second arg: typeof(TImpl)
        var implType = ResolveTypeofArg(descriptorArgs[1].Expression, context.SemanticModel);
        if (implType is null)
        {
            return;
        }

        // Third arg: ServiceLifetime.Singleton
        var lifetime = ResolveLifetimeArg(descriptorArgs[2].Expression, context.SemanticModel);
        if (lifetime is null)
        {
            return;
        }

        registrations.Add(new RegistrationEntry
        {
            ImplementationType = implType,
            Lifetime = lifetime.Value,
            Invocation = invocation,
        });
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
        var lifetime = ResolveLifetimeFromArg(invocation, implLifetimeParam, context.SemanticModel);
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

    /// <summary>
    /// Resolves a ServiceLifetime argument from an invocation by parameter name.
    /// </summary>
    private static Lifetime? ResolveLifetimeFromArg(
        InvocationExpressionSyntax invocation,
        IParameterSymbol parameter,
        SemanticModel semanticModel)
    {
        var argList = invocation.ArgumentList;
        if (argList is null)
        {
            return null;
        }

        // Check named arguments first.
        ArgumentSyntax? matchedArg = null;
        foreach (var arg in argList.Arguments)
        {
            if (arg.NameColon is { Name.Identifier.ValueText: var name } &&
                name == parameter.Name)
            {
                matchedArg = arg;
                break;
            }
        }

        // Fall back to positional argument.
        if (matchedArg is null)
        {
            var index = parameter.Ordinal;
            if (index < argList.Arguments.Count)
            {
                var arg = argList.Arguments[index];
                if (arg.NameColon is null)
                {
                    matchedArg = arg;
                }
            }
        }

        if (matchedArg is null)
        {
            return null;
        }

        return ResolveLifetimeArg(matchedArg.Expression, semanticModel);
    }

    private static void AnalyzeRegistrations(
        CompilationAnalysisContext context,
        List<RegistrationEntry> registrations)
    {
        if (registrations.Count == 0)
        {
            return;
        }

        // Build the registration map: type → lifetime (last wins).
        var lifetimeMap = new Dictionary<INamedTypeSymbol, Lifetime>(
            SymbolEqualityComparer.Default);

        foreach (var reg in registrations)
        {
            lifetimeMap[reg.ImplementationType] = reg.Lifetime;
        }

        // For each Singleton, check constructor parameters.
        foreach (var reg in registrations)
        {
            if (reg.Lifetime != Lifetime.Singleton)
            {
                continue;
            }

            AnalyzeSingleton(context, reg.ImplementationType, reg.Invocation, lifetimeMap);
        }
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

    private static INamedTypeSymbol? ResolveTypeofArg(
        ExpressionSyntax expr,
        SemanticModel semanticModel)
    {
        if (expr is not TypeOfExpressionSyntax typeofExpr)
        {
            return null;
        }

        var typeInfo = semanticModel.GetTypeInfo(typeofExpr.Type);
        return typeInfo.Type as INamedTypeSymbol;
    }

    private static Lifetime? ResolveLifetimeArg(
        ExpressionSyntax expr,
        SemanticModel semanticModel)
    {
        // Try constant value (enum member → int)
        var constantValue = semanticModel.GetConstantValue(expr);
        if (constantValue.HasValue && constantValue.Value is int intValue)
        {
            return intValue switch
            {
                0 => Lifetime.Singleton,
                1 => Lifetime.Scoped,
                2 => Lifetime.Transient,
                _ => null,
            };
        }

        // Try field symbol (ServiceLifetime.Singleton etc.)
        var symbol = semanticModel.GetSymbolInfo(expr).Symbol;
        if (symbol is IFieldSymbol field && field.HasConstantValue)
        {
            return field.ConstantValue switch
            {
                0 => Lifetime.Singleton,
                1 => Lifetime.Scoped,
                2 => Lifetime.Transient,
                _ => null,
            };
        }

        return null;
    }

    private static bool IsFactoryOrInstanceArg(
        ArgumentSyntax arg,
        SemanticModel semanticModel)
    {
        var expr = arg.Expression;

        // Lambda → factory
        if (expr is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
        {
            return true;
        }

        // Check if it's a delegate type (Func<IServiceProvider, T>)
        var typeInfo = semanticModel.GetTypeInfo(expr);
        if (typeInfo.Type is not null &&
            typeInfo.Type.TypeKind == TypeKind.Delegate)
        {
            return true;
        }

        // Object creation, method invocation → likely instance
        if (expr is ObjectCreationExpressionSyntax or InvocationExpressionSyntax)
        {
            return true;
        }

        return false;
    }
}
