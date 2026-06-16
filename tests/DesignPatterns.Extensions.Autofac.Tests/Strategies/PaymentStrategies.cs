using DesignPatterns.Behavioral;

namespace DesignPatterns.Extensions.Autofac.Tests.Strategies;

public interface IPaymentStrategy
{
    string Pay(decimal amount);
}

[RegisterStrategy<IPaymentStrategy>("alipay")]
public sealed class AlipayPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Alipay:{amount}";
}

[RegisterStrategy<IPaymentStrategy>("wechat")]
public sealed class WechatPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Wechat:{amount}";
}
