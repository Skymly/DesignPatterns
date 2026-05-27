using DesignPatterns.Behavioral;

namespace Strategy.Sample;

[RegisterStrategy<IPaymentStrategy>("alipay")]
public sealed class AlipayPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Alipay: {amount:C}";
}
