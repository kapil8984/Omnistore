namespace OrderInventory.Domain.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, 400) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized access.") : base(message, 401) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden access.") : base(message, 403) { }
}
