namespace DependencyInjection.Sample;

public interface IPaymentStrategy
{
    string Pay(decimal amount);
}
