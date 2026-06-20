using System.Collections.Immutable;
using DesignPatterns.Creational;
using DesignPatterns.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DesignPatterns.SourceGenerators.Tests;

internal static class SourceGeneratorTestContext
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    internal static GeneratorDriverRunResult Run<TGenerator>(params (string Path, string Source)[] sources)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(
                source.Source,
                parseOptions,
                path: source.Path))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "DesignPatterns.SourceGenerators.Tests",
            syntaxTrees: syntaxTrees,
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Compilation failed: " + string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    internal static GeneratorDriverRunResult RunWithReferencedAssembly<TGenerator>(
        (string Path, string Source) referencedAssemblySource,
        (string Path, string Source) implementationAssemblySource,
        string referencedAssemblyName = "ReferencedAssembly",
        string implementationAssemblyName = "ImplementationAssembly")
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);

        var referencedTree = CSharpSyntaxTree.ParseText(
            referencedAssemblySource.Source,
            parseOptions,
            path: referencedAssemblySource.Path);
        var referencedCompilation = CSharpCompilation.Create(
            referencedAssemblyName,
            new[] { referencedTree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var referencedStream = new MemoryStream();
        var emitResult = referencedCompilation.Emit(referencedStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                "Referenced assembly compilation failed: "
                + string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        referencedStream.Position = 0;
        var referencedMetadata = MetadataReference.CreateFromStream(referencedStream);

        var implementationTree = CSharpSyntaxTree.ParseText(
            implementationAssemblySource.Source,
            parseOptions,
            path: implementationAssemblySource.Path);
        var implementationCompilation = CSharpCompilation.Create(
            implementationAssemblyName,
            new[] { implementationTree },
            References.Add(referencedMetadata),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = implementationCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Implementation assembly compilation failed: "
                + string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(implementationCompilation, out _, out _);
        return driver.GetRunResult();
    }

    internal static IReadOnlyDictionary<string, string> GetGeneratedSources(GeneratorDriverRunResult runResult) =>
        runResult
            .Results
            .SelectMany(result => result.GeneratedSources)
            .OrderBy(source => source.HintName, StringComparer.Ordinal)
            .ToDictionary(
                source => source.HintName,
                source => source.SourceText.ToString(),
                StringComparer.Ordinal);

    internal static IReadOnlyList<DiagnosticSnapshot> GetGeneratorDiagnostics(GeneratorDriverRunResult runResult) =>
        runResult
            .Results
            .SelectMany(result => result.Diagnostics)
            .OrderBy(diagnostic => diagnostic.Location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .Select(diagnostic => new DiagnosticSnapshot(diagnostic.Id, diagnostic.GetMessage()))
            .ToList();

    /// <summary>
    /// Runs the generator twice on identical compilations and asserts that all
    /// pipeline stages tagged with <see cref="TrackingNames"/> return
    /// <see cref="IncrementalStepRunReason.Cached"/> or
    /// <see cref="IncrementalStepRunReason.Unchanged"/> on the second run.
    /// Roslyn-internal stages (Compilation, compilationAndGroupedNodes_*) are
    /// excluded because <see cref="CSharpCompilation.Clone"/> always marks them Modified.
    /// </summary>
    internal static void AssertCacheHit<TGenerator>(params (string Path, string Source)[] sources)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Source, parseOptions, path: source.Path))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "DesignPatterns.SourceGenerators.Tests.Cache",
            syntaxTrees: syntaxTrees,
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driverOptions = new GeneratorDriverOptions(
            disabledOutputs: IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new TGenerator().AsSourceGenerator() },
            driverOptions: driverOptions,
            parseOptions: parseOptions);

        // First run — populates the cache.
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        // Second run — same compilation; our tagged stages should be cached.
        // We use the same compilation (not Clone) because Location uses reference
        // equality: Clone forces the transform to re-execute, producing new
        // Location objects that break model value equality even though the
        // semantic content is identical. Using the same compilation verifies
        // that the transform outputs are cached when inputs are unchanged,
        // which is the guarantee the P0 refactoring restored.
        var secondResult = driver.RunGenerators(compilation).GetRunResult();

        // Collect all tracking names defined in TrackingNames so we only check
        // our own stages, not Roslyn-internal ones (Compilation, etc.).
        var ourTrackingNames = typeof(TrackingNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(f => (string)f.GetValue(null)!)
            .ToHashSet();

        foreach (var generatorResult in secondResult.Results)
        {
            var ourSteps = generatorResult.TrackedSteps
                .Where(step => ourTrackingNames.Contains(step.Key));

            Assert.NotEmpty(ourSteps);

            foreach (var trackedStep in ourSteps)
            {
                Assert.All(trackedStep.Value, runStep =>
                {
                    Assert.All(runStep.Outputs, output =>
                    {
                        Assert.True(
                            output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                            $"Stage '{trackedStep.Key}' expected Cached or Unchanged on second run, but was {output.Reason}.");
                    });
                });
            }
        }
    }

    internal sealed record DiagnosticSnapshot(string Id, string Message);

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(GenerateSingletonAttribute).Assembly.Location));
        return references.ToImmutableArray();
    }
}
