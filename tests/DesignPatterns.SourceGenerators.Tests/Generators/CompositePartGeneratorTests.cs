using DesignPatterns.SourceGenerators.Generators;

namespace DesignPatterns.SourceGenerators.Tests.Generators;

public sealed class CompositePartGeneratorTests
{
    private const string MenuUsings = """
        using System.Collections.Generic;
        using DesignPatterns.Structural;
        """;

    private const string MenuInterface = """

        namespace TestAssembly;

        public interface IMenuNode : ICompositeNode<IMenuNode>
        {
            string Title { get; }
        }
        """;

    [Fact]
    public Task GeneratesKeysAndCatalog()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root")]
            public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();

                public string Title => "Home";

                public IReadOnlyList<IMenuNode> Children => _children;

                public void SetChildren(IReadOnlyList<IMenuNode> children) =>
                    _children = children;
            }

            [CompositePart<IMenuNode>("settings", ParentKey = "root", Order = 10)]
            public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();

                public string Title => "Settings";

                public IReadOnlyList<IMenuNode> Children => _children;

                public void SetChildren(IReadOnlyList<IMenuNode> children) =>
                    _children = children;
            }

            [CompositePart<IMenuNode>("profile", ParentKey = "root", Order = 20)]
            public sealed class ProfileMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();

                public string Title => "Profile";

                public IReadOnlyList<IMenuNode> Children => _children;

                public void SetChildren(IReadOnlyList<IMenuNode> children) =>
                    _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesForestCatalogWithMultipleRoots()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root-a")]
            public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();

                public string Title => "Home";

                public IReadOnlyList<IMenuNode> Children => _children;

                public void SetChildren(IReadOnlyList<IMenuNode> children) =>
                    _children = children;
            }

            [CompositePart<IMenuNode>("root-b", Order = 10)]
            public sealed class SecondaryMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();

                public string Title => "Secondary";

                public IReadOnlyList<IMenuNode> Children => _children;

                public void SetChildren(IReadOnlyList<IMenuNode> children) =>
                    _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("ForestMenus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task GeneratesKeysAndCatalogWithNonGenericAttribute()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart("root", typeof(IMenuNode))]
            public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();

                public string Title => "Home";

                public IReadOnlyList<IMenuNode> Children => _children;

                public void SetChildren(IReadOnlyList<IMenuNode> children) =>
                    _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }

    [Fact]
    public Task ReportsDp010DuplicateKey()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root")]
            public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Home";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("root")]
            public sealed class DuplicateMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Duplicate";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp011UnknownParentKey()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("child", ParentKey = "missing")]
            public sealed class ChildMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Child";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp012Cycle()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("a", ParentKey = "b")]
            public sealed class MenuA : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "A";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("b", ParentKey = "a")]
            public sealed class MenuB : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "B";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp013ContractMismatch()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("broken")]
            public sealed class BrokenMenu
            {
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp014MissingParameterlessConstructor()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("custom")]
            public sealed class CustomMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                public CustomMenu(string title) => Title = title;

                public string Title { get; }

                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp015MissingBuildable()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root")]
            public sealed class HomeMenu : IMenuNode
            {
                public string Title => "Home";
                public IReadOnlyList<IMenuNode> Children { get; } = System.Array.Empty<IMenuNode>();
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task EmitsRegisterDiWhenDiIntegrationEnabled()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root")]
            public sealed class HomeMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Home";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("settings", ParentKey = "root", Order = 10)]
            public sealed class SettingsMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Settings";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            enableDiIntegration: true,
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratedSources(runResult));
    }
}
