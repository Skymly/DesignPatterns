using Composite.Sample;
using DesignPatterns.Structural;

Console.WriteLine("=== Catalog: MenuNodeCompositeCatalog.BuildRoot() ===");
var catalogRoot = MenuNodeCompositeCatalog.BuildRoot();

CompositeTraverser.Traverse(
    catalogRoot,
    (node, depth, _) => Console.WriteLine($"{new string(' ', depth * 2)}{node.Title}"));

Console.WriteLine();
Console.WriteLine($"Root key constant: {MenuNodeCompositeKeys.Root}");

Console.WriteLine();
Console.WriteLine("=== Manual: CompositeTreeBuilder<IMenuNode>() ===");
var manualRoot = new ManualMenuBranch("Home (manual)");
var manualTree = new CompositeTreeBuilder<IMenuNode>()
    .Branch(manualRoot, branch => branch
        .Leaf(new ManualMenuLeaf("Profile"))
        .Branch(new ManualMenuBranch("Settings"), settings => settings
            .Leaf(new ManualMenuLeaf("Account"))
            .Leaf(new ManualMenuLeaf("Privacy"))))
    .Build();

CompositeTraverser.Traverse(
    manualTree,
    (node, depth, _) => Console.WriteLine($"{new string(' ', depth * 2)}{node.Title}"));
