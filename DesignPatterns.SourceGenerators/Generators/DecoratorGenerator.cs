using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DesignPatterns.Diagnostics;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates decorator stacks for types marked with <c>[Decorator]</c>.
/// </summary>
[Generator]
public sealed class DecoratorGenerator : IIncrementalGenerator
{
    /// <summary>Metadata name for non-generic <c>DecoratorAttribute</c>.</summary>
    public const string DecoratorMetadataName = "DesignPatterns.Structural.DecoratorAttribute";

    /// <summary>Metadata name for generic <c>DecoratorAttribute&lt;TService&gt;</c>.</summary>
    public const string DecoratorGenericMetadataName = "DesignPatterns.Structural.DecoratorAttribute`1";

    private static readonly DiagnosticDescriptor DuplicateOrderDescriptor =
        DesignPatternsDiagnosticDescriptors.DecoratorDuplicateOrder;

    private static readonly DiagnosticDescriptor ContractMismatchDescriptor =
        DesignPatternsDiagnosticDescriptors.DecoratorContractMismatch;

    private static readonly DiagnosticDescriptor MissingDecoratorInterfaceDescriptor =
        DesignPatternsDiagnosticDescriptors.DecoratorMissingDecoratorInterface;

    private static readonly DiagnosticDescriptor MissingParameterlessConstructorDescriptor =
        DesignPatternsDiagnosticDescriptors.DecoratorMissingParameterlessConstructor;

    private static readonly DiagnosticDescriptor AsyncSignatureMismatchDescriptor =
        DesignPatternsDiagnosticDescriptors.DecoratorAsyncSignatureMismatch;

    private static readonly DiagnosticDescriptor DiNotResolvableDescriptor =
        DesignPatternsDiagnosticDescriptors.DecoratorDiNotResolvable;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            DecoratorMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false))
            .WithTrackingName(TrackingNames.DecoratorNonGenericTransform);

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            DecoratorGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true))
            .WithTrackingName(TrackingNames.DecoratorGenericTransform);

        var integrationOptions = GeneratorConfigHelper.CreateIntegrationOptionsProvider(context);

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()).Combine(integrationOptions),
            static (spc, source) => Execute(spc, source.Left.Left, source.Left.Right, source.Right));
    }

    private static Result<DecoratorRegistration> Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
    {
        if (context.TargetSymbol is not INamedTypeSymbol decoratorType)
        {
            return Result<DecoratorRegistration>.Empty;
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return Result<DecoratorRegistration>.Empty;
        }

        foreach (var attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not int order)
            {
                continue;
            }

            INamedTypeSymbol? serviceType = null;
            if (isGenericAttribute)
            {
                if (attribute.AttributeClass is { IsGenericType: true, TypeArguments.Length: > 0 })
                {
                    serviceType = attribute.AttributeClass.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else if (attribute.ConstructorArguments.Length > 1)
            {
                serviceType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
            }

            if (serviceType is null || serviceType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var serviceInfo = new ContractInfo(
                serviceType.ToDisplayString(),
                serviceType.Name,
                serviceType.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : serviceType.ContainingNamespace.ToDisplayString(),
                serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            return Result<DecoratorRegistration>.Success(new DecoratorRegistration(
                order,
                serviceInfo,
                decoratorType.Name,
                decoratorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ImplementsContract(decoratorType, serviceType),
                ImplementsDecoratorInterface(decoratorType, serviceType),
                ImplementsAsyncDecoratorInterface(decoratorType, serviceType),
                HasValidAsyncSignature(decoratorType, serviceType),
                HasPublicParameterlessConstructor(decoratorType),
                context.TargetNode.GetLocation()));
        }

        return Result<DecoratorRegistration>.Empty;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<Result<DecoratorRegistration>> nonGeneric,
        ImmutableArray<Result<DecoratorRegistration>> generic,
        GeneratorIntegrationOptions integrationOptions)
    {
        var registrations = ResultExtensions.ReportAndCollect(context, nonGeneric.Concat(generic));

        if (registrations.Count == 0)
        {
            return;
        }

        ReportValidationDiagnostics(context, registrations, integrationOptions);

        foreach (var group in registrations.GroupBy(static r => r.Service.FullyQualifiedName, StringComparer.Ordinal))
        {
            var serviceInfo = group.First().Service;

            var valid = group
                .Where(static r => r.ImplementsContract)
                .Where(static r => r.ImplementsDecoratorInterface || r.ImplementsAsyncDecoratorInterface)
                .Where(static r => r.HasPublicParameterlessConstructor)
                .Where(static r => !r.ImplementsAsyncDecoratorInterface || r.HasValidAsyncSignature)
                .GroupBy(static r => r.Order)
                .Where(static g => g.Count() == 1)
                .Select(static g => g.First())
                .OrderBy(static r => r.Order)
                .ThenBy(static r => r.DecoratorName, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitStack(context, serviceInfo, valid, integrationOptions);
        }
    }

    private static void ReportValidationDiagnostics(
        SourceProductionContext context,
        List<DecoratorRegistration> registrations,
        GeneratorIntegrationOptions integrationOptions)
    {
        foreach (var duplicateGroup in registrations
                     .GroupBy(static r => (r.Service.FullyQualifiedName, r.Order))
                     .Where(g => g.Count() > 1))
        {
            var serviceName = duplicateGroup.First().Service.FullyQualifiedName;
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOrderDescriptor,
                    registration.Location,
                    registration.Order,
                    serviceName));
            }
        }

        foreach (var registration in registrations.Where(static r => !r.ImplementsContract))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ContractMismatchDescriptor,
                registration.Location,
                registration.DecoratorName,
                registration.Service.FullyQualifiedName));
        }

        foreach (var registration in registrations.Where(static r => !r.ImplementsDecoratorInterface && !r.ImplementsAsyncDecoratorInterface))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingDecoratorInterfaceDescriptor,
                registration.Location,
                registration.DecoratorName,
                registration.Service.FullyQualifiedName));
        }

        foreach (var registration in registrations.Where(static r => r.ImplementsAsyncDecoratorInterface && !r.HasValidAsyncSignature))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AsyncSignatureMismatchDescriptor,
                registration.Location,
                registration.DecoratorName,
                registration.Service.FullyQualifiedName));
        }

        foreach (var registration in registrations.Where(static r => !r.HasPublicParameterlessConstructor))
        {
            if (integrationOptions.EnableDi)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiNotResolvableDescriptor,
                    registration.Location,
                    registration.DecoratorName));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingParameterlessConstructorDescriptor,
                    registration.Location,
                    registration.DecoratorName));
            }
        }
    }

    private static void EmitStack(
        SourceProductionContext context,
        ContractInfo serviceInfo,
        List<DecoratorRegistration> decorators,
        GeneratorIntegrationOptions integrationOptions)
    {
        var stackClassName = DecoratorStackSyntaxFactory.GetStackClassName(serviceInfo.Name);
        var decoratorEntries = decorators
            .Select(static d => (d.DecoratorFullyQualifiedDisplayString, d.ImplementsAsyncDecoratorInterface, d.ImplementsDecoratorInterface))
            .ToList();
        var hasAsyncDecorators = decorators.Any(static d => d.ImplementsAsyncDecoratorInterface);

        var compilationUnit = DecoratorStackSyntaxFactory.CreateStackCompilationUnit(
            serviceInfo.Namespace,
            stackClassName,
            serviceInfo.FullyQualifiedDisplayString,
            decoratorEntries,
            hasAsyncDecorators,
            integrationOptions);

        context.AddSource(
            $"{serviceInfo.Name}.{stackClassName}.g.cs",
            SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));

        var orderClassName = DecoratorStackSyntaxFactory.GetOrderClassName(serviceInfo.Name);
        var orderEntries = decorators
            .Select(static d => (ConstantName: d.DecoratorName, OrderValue: d.Order))
            .ToList();

        var orderCompilationUnit = DecoratorStackSyntaxFactory.CreateOrderCompilationUnit(
            serviceInfo.Namespace,
            orderClassName,
            orderEntries);

        context.AddSource(
            $"{serviceInfo.Name}.{orderClassName}.g.cs",
            SourceText.From(orderCompilationUnit.ToFullString(), Encoding.UTF8));
    }

    private static bool ImplementsContract(INamedTypeSymbol decoratorType, INamedTypeSymbol serviceType)
    {
        if (SymbolEqualityComparer.Default.Equals(decoratorType, serviceType))
        {
            return true;
        }

        foreach (var iface in decoratorType.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, serviceType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsDecoratorInterface(INamedTypeSymbol decoratorType, INamedTypeSymbol serviceType)
    {
        foreach (var iface in decoratorType.AllInterfaces)
        {
            if (iface.Name != "IDecorator" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], serviceType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsAsyncDecoratorInterface(INamedTypeSymbol decoratorType, INamedTypeSymbol serviceType)
    {
        foreach (var iface in decoratorType.AllInterfaces)
        {
            if (iface.Name != "IAsyncDecorator" || iface.TypeArguments.Length != 1)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], serviceType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasValidAsyncSignature(INamedTypeSymbol decoratorType, INamedTypeSymbol serviceType)
    {
        // Look for DecorateAsync method matching IAsyncDecorator<TService> signature
        foreach (var member in decoratorType.GetMembers())
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            if (method.Name != "DecorateAsync")
            {
                continue;
            }

            // Check return type is ValueTask<TService>
            if (method.ReturnType is not INamedTypeSymbol returnType)
            {
                continue;
            }

            if (returnType.Name != "ValueTask" || returnType.TypeArguments.Length != 1)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(returnType.TypeArguments[0], serviceType))
            {
                continue;
            }

            // Check parameters: (TService inner, CancellationToken cancellationToken = default)
            if (method.Parameters.Length != 2)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceType))
            {
                continue;
            }

            if (method.Parameters[1].Type.Name != "CancellationToken")
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol decoratorType) =>
        decoratorType.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);

    private sealed record DecoratorRegistration(
        int Order,
        ContractInfo Service,
        string DecoratorName,
        string DecoratorFullyQualifiedDisplayString,
        bool ImplementsContract,
        bool ImplementsDecoratorInterface,
        bool ImplementsAsyncDecoratorInterface,
        bool HasValidAsyncSignature,
        bool HasPublicParameterlessConstructor,
        Location Location);
}
