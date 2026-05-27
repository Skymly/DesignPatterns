using DesignPatterns.Behavioral;

namespace Strategy.Sample;

[RegisterStrategy<IPaymentStrategy>("wechat")]
public sealed class WechatPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Wechat: {amount:C}";
}
