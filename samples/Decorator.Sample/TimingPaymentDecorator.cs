using System.Diagnostics;
using DesignPatterns.Structural;

namespace Decorator.Sample;

[Decorator<IPaymentService>(20)]
public sealed class TimingPaymentDecorator : IPaymentService, IDecorator<IPaymentService>
{
    public IPaymentService Decorate(IPaymentService inner) => new Impl(inner);

    public string Pay(string method, decimal amount) =>
        throw new NotSupportedException("Use the decorated instance returned from Build.");

    private sealed class Impl(IPaymentService inner) : IPaymentService
    {
        public string Pay(string method, decimal amount)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return inner.Pay(method, amount);
            }
            finally
            {
                stopwatch.Stop();
                Console.WriteLine($"[timing] {stopwatch.ElapsedMilliseconds} ms");
            }
        }
    }
}
