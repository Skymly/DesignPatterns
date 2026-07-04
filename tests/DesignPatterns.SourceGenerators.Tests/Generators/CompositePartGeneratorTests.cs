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

    [Fact]
    public Task ReportsDp063MaxDepthExceeded()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            [CompositeSchema(MaxDepth = 2)]
            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("root")]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("mid", ParentKey = "root")]
            public sealed class MidMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Mid";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("leaf", ParentKey = "mid")]
            public sealed class LeafMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Leaf";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp063NotExceededWhenWithinLimit()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            [CompositeSchema(MaxDepth = 5)]
            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("root")]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("child", ParentKey = "root")]
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
    public Task ReportsDp064ChildTypeNotAllowed()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("root", AllowedChildTypes = new[] { typeof(AllowedMenu) })]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("allowed", ParentKey = "root")]
            public sealed class AllowedMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Allowed";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("disallowed", ParentKey = "root")]
            public sealed class DisallowedMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Disallowed";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp064AllowedWhenTypeInList()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("root", AllowedChildTypes = new[] { typeof(ChildMenu) })]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("child", ParentKey = "root")]
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
    public Task ReportsDp064NotCheckedWhenAllowedChildTypesNull()
    {
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root")]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("child", ParentKey = "root")]
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
    public Task ReportsDp065NodeCountExceeded()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            [CompositeSchema(MaxNodes = 3)]
            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("root")]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("a", ParentKey = "root")]
            public sealed class MenuA : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "A";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("b", ParentKey = "root")]
            public sealed class MenuB : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "B";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("c", ParentKey = "root")]
            public sealed class MenuC : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "C";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("d", ParentKey = "root")]
            public sealed class MenuD : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "D";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp065NotExceededWhenWithinLimit()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            [CompositeSchema(MaxNodes = 10)]
            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("root")]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("a", ParentKey = "root")]
            public sealed class MenuA : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "A";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task NoSchemaAttributeNoNewDiagnostics()
    {
        // Deep tree, many nodes, but no [CompositeSchema] → no DP063/DP065
        var source = MenuUsings + MenuInterface + """

            [CompositePart<IMenuNode>("root")]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("l1", ParentKey = "root")]
            public sealed class Level1 : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "L1";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("l2", ParentKey = "l1")]
            public sealed class Level2 : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "L2";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("l3", ParentKey = "l2")]
            public sealed class Level3 : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "L3";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp063ForestModeComputesPerTree()
    {
        // Two roots: one shallow, one deep → DP063 on the deep one
        var source = MenuUsings + """
            namespace TestAssembly;

            [CompositeSchema(MaxDepth = 2)]
            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart<IMenuNode>("shallow-root")]
            public sealed class ShallowRoot : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Shallow";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("deep-root")]
            public sealed class DeepRoot : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Deep";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("mid", ParentKey = "deep-root")]
            public sealed class MidMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Mid";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart<IMenuNode>("leaf", ParentKey = "mid")]
            public sealed class LeafMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Leaf";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }

    [Fact]
    public Task ReportsDp064WithNonGenericAttribute()
    {
        var source = MenuUsings + """
            namespace TestAssembly;

            public interface IMenuNode : ICompositeNode<IMenuNode>
            {
                string Title { get; }
            }
            """ + """

            [CompositePart("root", typeof(IMenuNode), AllowedChildTypes = new[] { typeof(AllowedMenu) })]
            public sealed class RootMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Root";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart("allowed", typeof(IMenuNode), ParentKey = "root")]
            public sealed class AllowedMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Allowed";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }

            [CompositePart("disallowed", typeof(IMenuNode), ParentKey = "root")]
            public sealed class DisallowedMenu : IMenuNode, ICompositeBuildable<IMenuNode>
            {
                private IReadOnlyList<IMenuNode> _children = System.Array.Empty<IMenuNode>();
                public string Title => "Disallowed";
                public IReadOnlyList<IMenuNode> Children => _children;
                public void SetChildren(IReadOnlyList<IMenuNode> children) => _children = children;
            }
            """;

        var runResult = SourceGeneratorTestContext.Run<CompositePartGenerator>(
            ("Menus.cs", source));

        return Verifier.Verify(SourceGeneratorTestContext.GetGeneratorDiagnostics(runResult));
    }
}
