using DependencyInjection.Sample;
using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

PaymentStrategyRegistry.RegisterDi(services);
ProductFactoryRegistry.RegisterDi(services, implementationLifetime: ServiceLifetime.Transient);
RequestContextHandlerPipeline.RegisterDi(services);

var provider = services.BuildServiceProvider();

Console.WriteLine("=== Strategy (RegisterDi) ===");
var strategies = provider.GetRequiredService<IStrategyRegistry<string, IPaymentStrategy>>();
Console.WriteLine(strategies.Get(PaymentStrategyKeys.Alipay).Pay(100m));
Console.WriteLine(strategies.Get(PaymentStrategyKeys.Wechat).Pay(50m));

Console.WriteLine();
Console.WriteLine("=== Factory (RegisterDi, transient) ===");
var factories = provider.GetRequiredService<IFactoryRegistry<string, IProductFactory>>();
Console.WriteLine(factories.Create(ProductFactoryKeys.Standard).Create());
Console.WriteLine(factories.Create(ProductFactoryKeys.Premium).Create());

Console.WriteLine();
Console.WriteLine("=== Handler pipeline (RegisterDi) ===");
var pipeline = provider.GetRequiredService<HandlerPipeline<RequestContext>>();
var context = new RequestContext();
await pipeline.InvokeAsync(context);
Console.WriteLine(context.Trace);
