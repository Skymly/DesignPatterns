using Strategy.Sample;

var registry = PaymentStrategyRegistry.Instance;

var alipay = registry.Get(PaymentStrategyKeys.Alipay);
var wechat = registry.Get(PaymentStrategyKeys.Wechat);

Console.WriteLine(alipay.Pay(100m));
Console.WriteLine(wechat.Pay(200m));

if (!registry.TryGet("unknown", out _))
{
    Console.WriteLine("Unknown key not found (expected).");
}
