using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        // Map each infrastructure exception to (status, localized-title-key, RFC type).
        // Title comes back through the localization service so the response respects
        // the request's culture; Detail keeps the caller-supplied message (it's their
        // string, not ours to translate).
        var (statusCode, titleKey, type) = exception switch
        {
            ValidationException   => ((int)HttpStatusCode.BadRequest,         LocalizedKeys.Infrastructure.ValidationError, "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
            UnauthorizedException => ((int)HttpStatusCode.Unauthorized,       LocalizedKeys.Auth.Unauthorized,              "https://tools.ietf.org/html/rfc7235#section-3.1"),
            ForbiddenException    => ((int)HttpStatusCode.Forbidden,          LocalizedKeys.Permissions.Forbidden,          "https://tools.ietf.org/html/rfc7231#section-6.5.3"),
            NotFoundException     => ((int)HttpStatusCode.NotFound,           LocalizedKeys.Infrastructure.NotFound,        "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
            ConflictException     => ((int)HttpStatusCode.Conflict,           LocalizedKeys.Infrastructure.Conflict,        "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
            // P0.8 / P1.4 — RowVersion mismatch means another writer beat us.
            // Map to 409 so the client knows to refresh-and-retry instead of
            // seeing the raw EF infra exception as a 500.
            DbUpdateConcurrencyException => ((int)HttpStatusCode.Conflict,    LocalizedKeys.Infrastructure.Conflict,        "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
            _                     => ((int)HttpStatusCode.InternalServerError, LocalizedKeys.Infrastructure.ServerError,    "https://tools.ietf.org/html/rfc7231#section-6.6.1")
        };

        // Resolve the localization service from the request's scope — the handler
        // itself is registered as a singleton (AddExceptionHandler default), so
        // constructor-injecting a scoped service would create a captive dependency.
        var localization = httpContext.RequestServices.GetService<ILocalizationService>();
        var title = localization?.GetString(titleKey) ?? titleKey;

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
