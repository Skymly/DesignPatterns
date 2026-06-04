using DesignPatterns.Behavioral;

namespace DependencyInjection.Sample;

[RegisterStrategy<IPaymentStrategy>("wechat")]
public sealed class WechatPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Wechat:{amount}";
}
