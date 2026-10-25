namespace API.Errors;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? Details { get; }

    public ApiException(int statusCode, string message, string? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details;
    }
}

public class NotFoundException : ApiException
{
    public NotFoundException(string message)
        : base(StatusCodes.Status404NotFound, message) { }
}

public class BadRequestException : ApiException
{
    public BadRequestException(string message)
        : base(StatusCodes.Status400BadRequest, message) { }
}

public class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "You are not authorized")
        : base(StatusCodes.Status401Unauthorized, message) { }
}

public class ForbiddenException : ApiException
{
    public ForbiddenException(string message = "Access denied")
        : base(StatusCodes.Status403Forbidden, message) { }
}
