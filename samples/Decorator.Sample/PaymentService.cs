namespace Decorator.Sample;

public sealed class PaymentService : IPaymentService
{
    public string Pay(string method, decimal amount) => $"{method}:{amount:F2}";
}
