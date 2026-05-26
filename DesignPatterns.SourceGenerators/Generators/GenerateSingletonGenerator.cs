using System;
using System.Linq;
using System.Text;
using DesignPatterns.SourceGenerators.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DesignPatterns.SourceGenerators.Generators;

/// <summary>
/// Generates <c>Lazy&lt;T&gt;</c>-backed singleton members for types marked with <c>[GenerateSingleton]</c>.
/// </summary>
[Generator]
public sealed class GenerateSingletonGenerator : IIncrementalGenerator
{
    /// <summary>Full metadata name of <c>GenerateSingletonAttribute</c>.</summary>
    public const string AttributeMetadataName = "DesignPatterns.Creational.GenerateSingletonAttribute";

    private static readonly DiagnosticDescriptor NotPartialDescriptor = new(
        id: "DP001",
        title: "GenerateSingleton requires a partial class",
        messageFormat: "Class '{0}' must be declared partial to receive generated singleton members",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidTargetDescriptor = new(
        id: "DP002",
        title: "GenerateSingleton target is invalid",
        messageFormat: "GenerateSingleton cannot be applied to '{0}'",
        category: "DesignPatterns.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeMetadataName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (ctx, _) => GetTargetInfo(ctx))
                .Where(static info => info is not null)
                .Select(static (info, _) => info!),
            static (spc, info) => Execute(spc, info));
    }

    private static SingletonTargetInfo? GetTargetInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        var symbol = context.TargetSymbol as INamedTypeSymbol;
        if (symbol is null || symbol.TypeKind == TypeKind.Error)
        {
            return null;
        }

        var threadSafe = true;
        foreach (var attribute in context.Attributes)
        {
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "ThreadSafe" && named.Value.Value is bool value)
                {
                    threadSafe = value;
                }
            }
        }

        return new SingletonTargetInfo(
            classDeclaration,
            symbol.Name,
            symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString(),
            threadSafe,
            symbol.IsStatic,
            symbol.TypeKind,
            classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static void Execute(SourceProductionContext context, SingletonTargetInfo info)
    {
        if (info.TypeKind == TypeKind.Struct)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidTargetDescriptor, info.Syntax.GetLocation(), info.ClassName));
            return;
        }

        if (info.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidTargetDescriptor, info.Syntax.GetLocation(), info.ClassName));
            return;
        }

        if (!info.IsPartial)
        {
            context.ReportDiagnostic(Diagnostic.Create(NotPartialDescriptor, info.Syntax.GetLocation(), info.ClassName));
            return;
        }

        var compilationUnit = SingletonSyntaxFactory.CreateCompilationUnit(
            info.NamespaceName,
            info.ClassName,
            info.ThreadSafe);

        var sourceText = SourceText.From(compilationUnit.ToFullString(), Encoding.UTF8);
        context.AddSource($"{info.ClassName}.Singleton.g.cs", sourceText);
    }

    private sealed class SingletonTargetInfo
    {
        public SingletonTargetInfo(
            ClassDeclarationSyntax syntax,
            string className,
            string? namespaceName,
            bool threadSafe,
            bool isStatic,
            TypeKind typeKind,
            bool isPartial)
        {
            Syntax = syntax;
            ClassName = className;
            NamespaceName = namespaceName;
            ThreadSafe = threadSafe;
            IsStatic = isStatic;
            TypeKind = typeKind;
            IsPartial = isPartial;
        }

        public ClassDeclarationSyntax Syntax { get; }

        public string ClassName { get; }

        public string? NamespaceName { get; }

        public bool ThreadSafe { get; }

        public bool IsStatic { get; }

        public TypeKind TypeKind { get; }

        public bool IsPartial { get; }
    }
}
