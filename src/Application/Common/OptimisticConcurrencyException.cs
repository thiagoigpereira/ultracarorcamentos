namespace Application.Common;

public sealed class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
