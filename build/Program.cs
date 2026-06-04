using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[UnsetVisualStudioEnvironmentVariables]
sealed class Build : NukeBuild
{
    [Parameter("Build configuration (Debug/Release)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Package version override")]
    readonly string? Version = Environment.GetEnvironmentVariable("VERSION");

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "DesignPatterns.slnx";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";
    AbsolutePath PackProject => Root / "DesignPatterns.Package" / "DesignPatterns.Package.csproj";

    static readonly string[] TestProjectRelativePaths =
    [
        "tests/DesignPatterns.Tests/DesignPatterns.Tests.csproj",
        "tests/DesignPatterns.SourceGenerators.Tests/DesignPatterns.SourceGenerators.Tests.csproj",
        "tests/DesignPatterns.Analyzers.Tests/DesignPatterns.Analyzers.Tests.csproj",
        "tests/DesignPatterns.Extensions.DependencyInjection.Tests/DesignPatterns.Extensions.DependencyInjection.Tests.csproj",
    ];

    const string ExpectedPackageId = "DesignPatterns";

    public static int Main() => Execute<Build>(x => x.Ci);

    Target Clean => _ => _
        .Executes(() =>
        {
            if (TestResultsDirectory.DirectoryExists())
            {
                TestResultsDirectory.DeleteDirectory();
            }

            TestResultsDirectory.CreateDirectory();
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(SolutionFile));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target BuildSamples => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (AbsolutePath sampleProject in Root.GlobFiles("samples/*/*.csproj"))
            {
                DotNetBuild(s => s
                    .SetProjectFile(sampleProject)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore());
            }
        });

    Target UnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (string relativePath in TestProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    throw new InvalidOperationException($"Test project not found: {projectFile}");
                }

                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .SetNoBuild(true)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx"));
            }
        });

    Target Pack => _ => _
        .DependsOn(UnitTest)
        .DependsOn(BuildSamples)
        .Executes(() =>
        {
            PackageOutputDirectory.CreateOrCleanDirectory();

            DotNetPack(s =>
            {
                s = s
                    .SetProject(PackProject)
                    .SetConfiguration(Configuration)
                    .SetProperty("PackageOutputPath", PackageOutputDirectory)
                    .SetProperty("ContinuousIntegrationBuild", "true");

                if (!string.IsNullOrWhiteSpace(Version))
                {
                    s = s.SetVersion(Version);
                }

                return s;
            });
        });

    Target PackVerify => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            IReadOnlyCollection<AbsolutePath> nupkgs = PackageOutputDirectory.GlobFiles("*.nupkg");
            if (nupkgs.Count == 0)
            {
                throw new InvalidOperationException($"No packages found in {PackageOutputDirectory}");
            }

            AbsolutePath nupkg = nupkgs.SingleOrDefault(p => p.Name.StartsWith(ExpectedPackageId + ".", StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Expected a single '{ExpectedPackageId}.*.nupkg' package, found: {string.Join(", ", nupkgs.Select(p => p.Name))}");

            using ZipArchive archive = ZipFile.OpenRead(nupkg);
            HashSet<string> entries = archive.Entries
                .Select(e => e.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool hasRuntime = entries.Any(e => e.StartsWith("lib/netstandard2.0/", StringComparison.OrdinalIgnoreCase)
                && e.EndsWith("DesignPatterns.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasRuntime, $"{ExpectedPackageId}: missing lib/netstandard2.0/DesignPatterns.dll");

            bool hasSourceGenerator = entries.Any(e => e.StartsWith("analyzers/dotnet/cs/", StringComparison.OrdinalIgnoreCase)
                && e.Contains("DesignPatterns.SourceGenerators", StringComparison.OrdinalIgnoreCase)
                && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasSourceGenerator, $"{ExpectedPackageId}: missing DesignPatterns.SourceGenerators analyzer DLL");

            bool hasAnalyzers = entries.Any(e => e.StartsWith("analyzers/dotnet/cs/", StringComparison.OrdinalIgnoreCase)
                && e.Contains("DesignPatterns.Analyzers", StringComparison.OrdinalIgnoreCase)
                && e.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasAnalyzers, $"{ExpectedPackageId}: missing DesignPatterns.Analyzers DLL");

            Assert.True(entries.Contains("README.md"), $"{ExpectedPackageId}: missing package README.md");
            Assert.True(entries.Contains("LICENSE") || entries.Contains("LICENSE.md"), $"{ExpectedPackageId}: missing LICENSE");
        });

    Target Ci => _ => _
        .DependsOn(UnitTest)
        .DependsOn(BuildSamples);

    Target CiPack => _ => _
        .DependsOn(PackVerify);
}
