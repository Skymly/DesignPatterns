using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignPatterns.Analyzers.Di;

/// <summary>
/// Type→lifetime map built from explicit container registrations
/// (MSDI Add*/TryAdd, Autofac RegisterType/Register) and attributed
/// <c>RegisterDi</c> expansions (holder-category matching).
/// </summary>
internal sealed class DiRegistrationMap
{
    private DiRegistrationMap(
        IReadOnlyList<DiRegistration> entries,
        IReadOnlyDictionary<INamedTypeSymbol, Lifetime> lifetimes)
    {
        Entries = entries;
        Lifetimes = lifetimes;
    }

    public IReadOnlyList<DiRegistration> Entries { get; }

    /// <summary>
    /// Lifetime by implementation type. When the same type appears more than
    /// once, later entries win; attributed <c>RegisterDi</c> expansions are
    /// always applied after explicit container registrations.
    /// </summary>
    public IReadOnlyDictionary<INamedTypeSymbol, Lifetime> Lifetimes { get; }

    /// <summary>
    /// Walks all invocation expressions in <paramref name="compilation"/> and
    /// collects explicit container registrations plus attributed
    /// <c>RegisterDi</c> expansions into a map.
    /// </summary>
    public static DiRegistrationMap Build(Compilation compilation)
    {
        var attributedTypes = AttributedRegistration.CollectByCategory(compilation);
        var builder = new DiRegistrationMapBuilder(attributedTypes);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                builder.TryCollect(invocation, model);
            }
        }

        return builder.Build();
    }

    internal static DiRegistrationMap FromEntries(IEnumerable<DiRegistration> entries)
    {
        var list = entries.ToImmutableArray();
        var lifetimes = new Dictionary<INamedTypeSymbol, Lifetime>(SymbolEqualityComparer.Default);
        foreach (var entry in list)
        {
            lifetimes[entry.ImplementationType] = entry.Lifetime;
        }

        return new DiRegistrationMap(list, lifetimes);
    }
}

/// <summary>
/// Incrementally collects explicit container registrations and attributed
/// <c>RegisterDi</c> expansions for <see cref="DiRegistrationMap"/>.
/// </summary>
internal sealed class DiRegistrationMapBuilder
{
    private readonly List<DiRegistration> _explicitEntries = new();
    private readonly List<DiRegistration> _attributedEntries = new();
    private readonly IReadOnlyDictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> _attributedTypesByCategory;

    public DiRegistrationMapBuilder(
        IReadOnlyDictionary<RegistrationCategory, HashSet<INamedTypeSymbol>> attributedTypesByCategory)
    {
        _attributedTypesByCategory = attributedTypesByCategory;
    }

    /// <summary>
    /// Attempts to collect an explicit MSDI/Autofac registration or an
    /// attributed <c>RegisterDi</c> expansion from <paramref name="invocation"/>.
    /// Returns <see langword="true"/> when one or more entries were added.
    /// </summary>
    public bool TryCollect(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var beforeExplicit = _explicitEntries.Count;
        var beforeAttributed = _attributedEntries.Count;

        if (methodName is "AddSingleton" or "AddScoped" or "AddTransient")
        {
            CollectDirectRegistration(invocation, methodName, semanticModel);
        }
        else if (methodName == "TryAdd")
        {
            CollectTryAddRegistration(invocation, semanticModel);
        }
        else if (methodName == "RegisterType")
        {
            CollectAutofacRegisterType(invocation, semanticModel);
        }
        else if (methodName == "Register")
        {
            CollectAutofacRegisterDelegate(invocation, semanticModel);
        }
        else if (methodName == "RegisterDi")
        {
            CollectRegisterDiRegistration(invocation, semanticModel);
        }

        return _explicitEntries.Count > beforeExplicit || _attributedEntries.Count > beforeAttributed;
    }

    /// <summary>
    /// Builds the map with attributed <c>RegisterDi</c> entries after explicit
    /// ones so RegisterDi lifetimes overlay explicit registrations for the same
    /// type (behaviour freeze vs the pre-extraction Captive Dependency path).
    /// </summary>
    public DiRegistrationMap Build() =>
        DiRegistrationMap.FromEntries(_explicitEntries.Concat(_attributedEntries));

    /// <summary>
    /// Collects registrations from generated <c>RegisterDi</c> calls.
    /// Extracts the implementation lifetime and applies it to the types
    /// bearing the DesignPatterns registration attribute that matches
    /// the RegisterDi holder's pattern (Strategy/Factory/EventHandler/Decorator/Composite).
    /// </summary>
    private void CollectRegisterDiRegistration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
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
            invocation, implLifetimeParam, semanticModel);
        var containingTypeName = methodSymbol.ContainingType?.Name ?? "";
        if (lifetime is null)
        {
            // Use default: Factory → Transient, others → Singleton.
            lifetime = AttributedRegistration.DefaultLifetimeForHolder(containingTypeName);
        }

        // Match the RegisterDi holder type name to the correct attribute category.
        // This prevents cross-pattern contamination (e.g. a Strategy RegisterDi call
        // should not apply its lifetime to Factory implementation types).
        var category = AttributedRegistration.MatchCategoryByHolderName(containingTypeName);
        if (category is null)
        {
            return;
        }

        if (!_attributedTypesByCategory.TryGetValue(category.Value, out var typesForCategory))
        {
            return;
        }

        // Add only the implementation types of the matched category.
        foreach (var implType in typesForCategory)
        {
            if (AutofacRegistration.IsOpenGeneric(implType))
            {
                continue;
            }

            _attributedEntries.Add(new DiRegistration(implType, lifetime.Value, invocation));
        }
    }

    private void CollectDirectRegistration(
        InvocationExpressionSyntax invocation,
        string methodName,
        SemanticModel semanticModel)
    {
        var lifetime = methodName switch
        {
            "AddSingleton" => Lifetime.Singleton,
            "AddScoped" => Lifetime.Scoped,
            "AddTransient" => Lifetime.Transient,
            _ => Lifetime.Transient,
        };

        var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (methodSymbol is null)
        {
            return;
        }

        INamedTypeSymbol? implType = null;
        var args = invocation.ArgumentList?.Arguments ?? default;

        if (methodSymbol.TypeArguments.Length == 2 &&
            methodSymbol.TypeArguments[1] is INamedTypeSymbol implFromGeneric)
        {
            implType = implFromGeneric;
        }
        else if (methodSymbol.TypeArguments.Length == 1 &&
                 methodSymbol.TypeArguments[0] is INamedTypeSymbol singleGeneric)
        {
            if (args.Count > 0 && IsFactoryOrInstanceArg(args[0], semanticModel))
            {
                if (AutofacRegistration.IsOpenGeneric(singleGeneric))
                {
                    return;
                }

                _explicitEntries.Add(new DiRegistration(
                    singleGeneric,
                    lifetime,
                    invocation,
                    skipConstructorAnalysis: true));
                return;
            }

            implType = singleGeneric;
        }

        if (implType is null || AutofacRegistration.IsOpenGeneric(implType))
        {
            return;
        }

        _explicitEntries.Add(new DiRegistration(implType, lifetime, invocation));
    }

    private void CollectTryAddRegistration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var argList = invocation.ArgumentList;
        if (argList is null || argList.Arguments.Count == 0)
        {
            return;
        }

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
        var implType = ResolveTypeofArg(descriptorArgs[1].Expression, semanticModel);
        if (implType is null)
        {
            return;
        }

        var lifetime = LifetimeResolution.TryResolve(descriptorArgs[2].Expression, semanticModel);
        if (lifetime is null)
        {
            return;
        }

        _explicitEntries.Add(new DiRegistration(implType, lifetime.Value, invocation));
    }

    private void CollectAutofacRegisterType(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (!AutofacRegistration.IsAutofacMethod(methodSymbol))
        {
            return;
        }

        INamedTypeSymbol? implType = null;
        if (methodSymbol!.TypeArguments.Length == 1 &&
            methodSymbol.TypeArguments[0] is INamedTypeSymbol generic)
        {
            implType = generic;
        }
        else
        {
            var args = invocation.ArgumentList?.Arguments ?? default;
            if (args.Count == 1)
            {
                implType = ResolveTypeofArg(args[0].Expression, semanticModel);
            }
        }

        if (implType is null || AutofacRegistration.IsOpenGeneric(implType))
        {
            return;
        }

        _explicitEntries.Add(new DiRegistration(
            implType,
            AutofacRegistration.ResolveChainLifetime(invocation),
            invocation));
    }

    private void CollectAutofacRegisterDelegate(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (!AutofacRegistration.IsAutofacMethod(methodSymbol))
        {
            return;
        }

        if (methodSymbol!.TypeArguments.Length != 1 ||
            methodSymbol.TypeArguments[0] is not INamedTypeSymbol serviceType ||
            AutofacRegistration.IsOpenGeneric(serviceType))
        {
            return;
        }

        var args = invocation.ArgumentList?.Arguments ?? default;
        if (args.Count == 0 || args[0].Expression is not AnonymousFunctionExpressionSyntax)
        {
            return;
        }

        _explicitEntries.Add(new DiRegistration(
            serviceType,
            AutofacRegistration.ResolveChainLifetime(invocation),
            invocation,
            skipConstructorAnalysis: true));
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

    private static bool IsFactoryOrInstanceArg(
        ArgumentSyntax arg,
        SemanticModel semanticModel)
    {
        var expr = arg.Expression;

        if (expr is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
        {
            return true;
        }

        var typeInfo = semanticModel.GetTypeInfo(expr);
        if (typeInfo.Type is not null &&
            typeInfo.Type.TypeKind == TypeKind.Delegate)
        {
            return true;
        }

        if (expr is ObjectCreationExpressionSyntax or InvocationExpressionSyntax)
        {
            return true;
        }

        return false;
    }
}
