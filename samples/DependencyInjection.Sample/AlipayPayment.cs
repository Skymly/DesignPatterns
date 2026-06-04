using DesignPatterns.Behavioral;

namespace DependencyInjection.Sample;

[RegisterStrategy<IPaymentStrategy>("alipay")]
public sealed class AlipayPayment : IPaymentStrategy
{
    public string Pay(decimal amount) => $"Alipay:{amount}";
}
