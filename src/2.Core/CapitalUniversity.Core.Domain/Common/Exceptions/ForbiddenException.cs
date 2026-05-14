namespace CapitalUniversity.Core.Domain.Common.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden access.") : base(message)
    {
    }
}
