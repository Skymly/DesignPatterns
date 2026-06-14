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

    [Parameter("NuGet API key (required for nuget.org Publish)")]
    readonly string? NuGetApiKey =
        Environment.GetEnvironmentVariable("NUGET_API_KEY")
        ?? Environment.GetEnvironmentVariable("APIKEY");

    [Parameter("GitHub token with packages:write (required for GitHub Packages Publish)")]
    readonly string? GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "DesignPatterns.slnx";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";
    AbsolutePath NuGetSmokeDirectory => Root / "eng" / "nuget-smoke";
    AbsolutePath NuGetSmokeConsumerProject => NuGetSmokeDirectory / "MetaPackage.Consumer" / "MetaPackage.Consumer.csproj";
    AbsolutePath NuGetSmokeLocalConfig => NuGetSmokeDirectory / "nuget.config.local";

    [Parameter("NuGet consumer smoke feed: Local (artifacts/package) or Published (nuget.org)")]
    readonly NuGetConsumerFeed ConsumerFeed = NuGetConsumerFeed.Local;

    AbsolutePath PackProject => Root / "DesignPatterns.Package" / "DesignPatterns.Package.csproj";

    static readonly string[] TestProjectRelativePaths =
    [
        "tests/DesignPatterns.Tests/DesignPatterns.Tests.csproj",
        "tests/DesignPatterns.SourceGenerators.Tests/DesignPatterns.SourceGenerators.Tests.csproj",
        "tests/DesignPatterns.Analyzers.Tests/DesignPatterns.Analyzers.Tests.csproj",
        "tests/DesignPatterns.Extensions.DependencyInjection.Tests/DesignPatterns.Extensions.DependencyInjection.Tests.csproj",
    ];

    const string ExpectedPackageId = "Skymly.DesignPatterns";

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
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx")
                    .SetDataCollector("XPlat Code Coverage"));
            }
        });

    Target Pack => _ => _
        .DependsOn(UnitTest)
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
            AbsolutePath nupkg = GetExpectedPackage();

            using ZipArchive archive = ZipFile.OpenRead(nupkg);
            HashSet<string> entries = archive.Entries
                .Select(e => e.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool hasRuntime = entries.Any(e => e.StartsWith("lib/netstandard2.0/", StringComparison.OrdinalIgnoreCase)
                && e.EndsWith("DesignPatterns.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasRuntime, $"{ExpectedPackageId}: missing lib/netstandard2.0/DesignPatterns.dll");

            bool hasRuntimeNet8 = entries.Any(e => e.StartsWith("lib/net8.0/", StringComparison.OrdinalIgnoreCase)
                && e.EndsWith("DesignPatterns.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasRuntimeNet8, $"{ExpectedPackageId}: missing lib/net8.0/DesignPatterns.dll");

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

    Target NuGetConsumerSmoke => _ => _
        .DependsOn(PackVerify)
        .Executes(() =>
        {
            Assert.FileExists(NuGetSmokeConsumerProject, $"Consumer project not found: {NuGetSmokeConsumerProject}");

            string packageVersion = GetPackageVersion(GetExpectedPackage());
            string? previousNuGetConfig = Environment.GetEnvironmentVariable("NUGET_CONFIG");

            if (ConsumerFeed == NuGetConsumerFeed.Local)
            {
                WriteLocalNuGetConfig();
                Environment.SetEnvironmentVariable("NUGET_CONFIG", NuGetSmokeLocalConfig);
            }

            try
            {
                DotNetBuild(s => s
                    .SetProjectFile(NuGetSmokeConsumerProject)
                    .SetConfiguration(Configuration)
                    .SetProperty("DesignPatternsConsumerPackageVersion", packageVersion));

                DotNetRun(s => s
                    .SetProjectFile(NuGetSmokeConsumerProject)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild());
            }
            finally
            {
                Environment.SetEnvironmentVariable("NUGET_CONFIG", previousNuGetConfig);
            }
        });

    Target PackConsumerVerify => _ => _
        .DependsOn(NuGetConsumerSmoke);

    Target NuGetConsumerSmokePublished => _ => _
        .DependsOn(NuGetConsumerSmoke);

    Target Ci => _ => _
        .DependsOn(UnitTest);

    Target Test => _ => _
        .DependsOn(UnitTest);

    Target Publish => _ => _
        .DependsOn(Test, PackVerify)
        .Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey) || !string.IsNullOrWhiteSpace(GitHubToken))
        .Executes(() =>
        {
            AbsolutePath packages = PackageOutputDirectory / "*.nupkg";

            if (!string.IsNullOrWhiteSpace(NuGetApiKey))
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(packages)
                    .SetApiKey(NuGetApiKey)
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .EnableSkipDuplicate());
            }

            if (!string.IsNullOrWhiteSpace(GitHubToken))
            {
                DotNetNuGetPush(s => s
                    .SetTargetPath(packages)
                    .SetApiKey(GitHubToken)
                    .SetSource("https://nuget.pkg.github.com/Skymly/index.json")
                    .EnableSkipDuplicate());
            }
        });

    Target CiPack => _ => _
        .DependsOn(PackConsumerVerify);

    void WriteLocalNuGetConfig()
    {
        string localFeed = PackageOutputDirectory.ToString().Replace('\\', '/');
        File.WriteAllText(NuGetSmokeLocalConfig, $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{{localFeed}}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
    }

    AbsolutePath GetExpectedPackage()
    {
        IReadOnlyCollection<AbsolutePath> nupkgs = PackageOutputDirectory.GlobFiles("*.nupkg");
        if (nupkgs.Count == 0)
        {
            throw new InvalidOperationException($"No packages found in {PackageOutputDirectory}");
        }

        return nupkgs.SingleOrDefault(p => p.Name.StartsWith(ExpectedPackageId + ".", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Expected a single '{ExpectedPackageId}.*.nupkg' package, found: {string.Join(", ", nupkgs.Select(p => p.Name))}");
    }

    static string GetPackageVersion(AbsolutePath nupkg)
    {
        string fileName = nupkg.Name;
        string prefix = ExpectedPackageId + ".";
        const string suffix = ".nupkg";
        return fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
    }
}

enum NuGetConsumerFeed
{
    Local,
    Published
}
