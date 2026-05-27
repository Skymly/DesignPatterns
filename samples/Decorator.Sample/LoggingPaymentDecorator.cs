using DesignPatterns.Structural;

namespace Decorator.Sample;

[Decorator<IPaymentService>(10)]
public sealed class LoggingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
{
    public IPaymentService Decorate(IPaymentService inner) => new Impl(inner);

    public string Pay(string method, decimal amount) =>
        throw new NotSupportedException("Use the decorated instance returned from Build.");

    private sealed class Impl(IPaymentService inner) : IPaymentService
    {
        public string Pay(string method, decimal amount)
        {
            Console.WriteLine($"[log] Pay {method} {amount}");
            return inner.Pay(method, amount);
        }
    }
}
