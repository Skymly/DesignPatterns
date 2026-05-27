using Decorator.Sample;

var core = new PaymentService();
var decorated = PaymentServiceDecoratorStack.Build(core);

Console.WriteLine("Core only:");
Console.WriteLine(core.Pay("card", 42m));

Console.WriteLine();
Console.WriteLine("Decorated stack (log outer, timing inner):");
Console.WriteLine(decorated.Pay("card", 42m));
