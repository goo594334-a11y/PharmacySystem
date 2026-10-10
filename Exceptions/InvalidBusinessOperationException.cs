namespace PharmacySystem.Exceptions;

public class InvalidBusinessOperationException : Exception
{
    public InvalidBusinessOperationException(string message) : base(message)
    {
    }
}
