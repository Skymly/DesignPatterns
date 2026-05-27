namespace Decorator.Sample;

public interface IPaymentService
{
    string Pay(string method, decimal amount);
}
