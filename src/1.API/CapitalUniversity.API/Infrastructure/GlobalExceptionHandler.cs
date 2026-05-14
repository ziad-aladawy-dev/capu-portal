using CapitalUniversity.Core.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CapitalUniversity.API.Infrastructure;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, type) = exception switch
        {
            ValidationException => ((int)HttpStatusCode.BadRequest, "Validation Error", "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            UnauthorizedException => ((int)HttpStatusCode.Unauthorized, "Unauthorized", "https://tools.ietf.org/html/rfc7235#section-3.1"),
            ForbiddenException => ((int)HttpStatusCode.Forbidden, "Forbidden", "https://tools.ietf.org/html/rfc7231#section-6.5.3"),
            NotFoundException => ((int)HttpStatusCode.NotFound, "Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
            ConflictException => ((int)HttpStatusCode.Conflict, "Conflict", "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
            _ => ((int)HttpStatusCode.InternalServerError, "Server Error", "https://tools.ietf.org/html/rfc7231#section-6.6.1")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = exception.Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
