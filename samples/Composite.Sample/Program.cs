using Composite.Sample;
using DesignPatterns.Structural;

var root = MenuNodeCompositeCatalog.BuildRoot();

Console.WriteLine("=== Menu tree (depth-first) ===");
CompositeTraverser.Traverse(
    root,
    (node, depth, _) => Console.WriteLine($"{new string(' ', depth * 2)}{node.Title}"));

Console.WriteLine();
Console.WriteLine("=== Leaf items only ===");
CompositeTraverser.Traverse(
    root,
    (node, depth, _) => Console.WriteLine($"{new string(' ', depth * 2)}{node.Title}"),
    new CompositeTraversalOptions<IMenuNode> { VisitLeavesOnly = true });

Console.WriteLine();
Console.WriteLine($"Root key constant: {MenuNodeCompositeKeys.Root}");
