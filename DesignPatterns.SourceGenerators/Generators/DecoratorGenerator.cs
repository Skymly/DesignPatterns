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

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var nonGeneric = context.SyntaxProvider.ForAttributeWithMetadataName(
            DecoratorMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: false));

        var generic = context.SyntaxProvider.ForAttributeWithMetadataName(
            DecoratorGenericMetadataName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Transform(ctx, isGenericAttribute: true));

        context.RegisterSourceOutput(
            nonGeneric.Collect().Combine(generic.Collect()),
            static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static DecoratorRegistration? Transform(GeneratorAttributeSyntaxContext context, bool isGenericAttribute)
    {
        if (context.TargetSymbol is not INamedTypeSymbol decoratorType)
        {
            return null;
        }

        if (context.Attributes.IsDefaultOrEmpty)
        {
            return null;
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

            return new DecoratorRegistration(
                order,
                serviceType,
                decoratorType,
                context.TargetNode.GetLocation());
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<DecoratorRegistration?> nonGeneric,
        ImmutableArray<DecoratorRegistration?> generic)
    {
        var registrations = nonGeneric
            .Concat(generic)
            .Where(static r => r is not null)
            .Cast<DecoratorRegistration>()
            .ToList();

        if (registrations.Count == 0)
        {
            return;
        }

        ReportValidationDiagnostics(context, registrations);

        foreach (var group in registrations.GroupBy(static r => r.ServiceType, SymbolEqualityComparer.Default))
        {
            if (group.Key is not INamedTypeSymbol serviceType)
            {
                continue;
            }

            var valid = group
                .Where(r => ImplementsContract(r.DecoratorType, serviceType))
                .Where(r => ImplementsDecoratorInterface(r.DecoratorType, serviceType))
                .Where(r => HasPublicParameterlessConstructor(r.DecoratorType))
                .GroupBy(r => r.Order)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .OrderBy(r => r.Order)
                .ThenBy(r => r.DecoratorType.Name, StringComparer.Ordinal)
                .ToList();

            if (valid.Count == 0)
            {
                continue;
            }

            EmitStack(context, serviceType, valid);
        }
    }

    private static void ReportValidationDiagnostics(
        SourceProductionContext context,
        List<DecoratorRegistration> registrations)
    {
        foreach (var duplicateGroup in registrations
                     .GroupBy(static r => (r.ServiceType, r.Order))
                     .Where(g => g.Count() > 1))
        {
            var serviceName = duplicateGroup.First().ServiceType.ToDisplayString();
            foreach (var registration in duplicateGroup)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOrderDescriptor,
                    registration.Location,
                    registration.Order,
                    serviceName));
            }
        }

        foreach (var registration in registrations.Where(r => !ImplementsContract(r.DecoratorType, r.ServiceType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ContractMismatchDescriptor,
                registration.Location,
                registration.DecoratorType.Name,
                registration.ServiceType.ToDisplayString()));
        }

        foreach (var registration in registrations.Where(r => !ImplementsDecoratorInterface(r.DecoratorType, r.ServiceType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingDecoratorInterfaceDescriptor,
                registration.Location,
                registration.DecoratorType.Name,
                registration.ServiceType.ToDisplayString()));
        }

        foreach (var registration in registrations.Where(r => !HasPublicParameterlessConstructor(r.DecoratorType)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingParameterlessConstructorDescriptor,
                registration.Location,
                registration.DecoratorType.Name));
        }
    }

    private static void EmitStack(
        SourceProductionContext context,
        INamedTypeSymbol serviceType,
        List<DecoratorRegistration> decorators)
    {
        var namespaceName = serviceType.ContainingNamespace.IsGlobalNamespace
            ? null
            : serviceType.ContainingNamespace.ToDisplayString();

        var serviceTypeName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var stackClassName = DecoratorStackSyntaxFactory.GetStackClassName(serviceType);
        var decoratorTypeNames = decorators
            .Select(d => d.DecoratorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToList();

        var compilationUnit = DecoratorStackSyntaxFactory.CreateStackCompilationUnit(
            namespaceName,
            stackClassName,
            serviceTypeName,
            decoratorTypeNames);

        context.AddSource(
            $"{serviceType.Name}.{stackClassName}.g.cs",
            SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8));

        var orderClassName = DecoratorStackSyntaxFactory.GetOrderClassName(serviceType);
        var orderEntries = decorators
            .Select(d => (ConstantName: d.DecoratorType.Name, OrderValue: d.Order))
            .ToList();

        var orderCompilationUnit = DecoratorStackSyntaxFactory.CreateOrderCompilationUnit(
            namespaceName,
            orderClassName,
            orderEntries);

        context.AddSource(
            $"{serviceType.Name}.{orderClassName}.g.cs",
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

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol decoratorType) =>
        decoratorType.InstanceConstructors.Any(static c =>
            c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);

    private sealed class DecoratorRegistration
    {
        public DecoratorRegistration(
            int order,
            INamedTypeSymbol serviceType,
            INamedTypeSymbol decoratorType,
            Location location)
        {
            Order = order;
            ServiceType = serviceType;
            DecoratorType = decoratorType;
            Location = location;
        }

        public int Order { get; }

        public INamedTypeSymbol ServiceType { get; }

        public INamedTypeSymbol DecoratorType { get; }

        public Location Location { get; }
    }
}
