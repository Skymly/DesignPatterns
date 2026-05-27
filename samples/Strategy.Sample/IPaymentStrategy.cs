namespace Strategy.Sample;

public interface IPaymentStrategy
{
    string Pay(decimal amount);
}
