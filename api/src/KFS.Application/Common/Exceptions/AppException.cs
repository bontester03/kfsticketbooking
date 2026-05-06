namespace KFS.Application.Common.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public object? Extra { get; }

    public AppException(string code, string message, int statusCode = 400, object? extra = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Extra = extra;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base("not_found", $"{entity} '{key}' not found", 404) { }
}

public class ConflictException : AppException
{
    public ConflictException(string code, string message, object? extra = null)
        : base(code, message, 409, extra) { }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized")
        : base("unauthorized", message, 401) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden")
        : base("forbidden", message, 403) { }
}
