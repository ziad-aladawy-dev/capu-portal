using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace CapitalUniversity.API.Infrastructure;

/// <summary>
/// Single funnel for every uncaught exception. Maps domain exceptions to a
/// localized <see cref="ProblemDetails"/> response with a stable shape:
/// status / title / detail / type / instance plus a <c>traceId</c> extension and
/// (for validation failures) an <c>errors</c> dictionary.
///
/// <para>
/// Runtime Hardening Plan §2.3 + §4.1: client-originated exceptions
/// (ValidationException, NotFoundException, ForbiddenException, UnauthorizedException,
/// ConflictException) log at Warning, not Error — they are not application
/// failures and used to flood the Error channel. Infrastructure / runtime
/// failures still log at Error so on-call still sees them. Both the response's
/// Title and Detail are localized when the underlying message is a known key
/// from <see cref="LocalizedKeys"/>; otherwise the literal is passed through.
/// </para>
///
/// <para>
/// Runtime Hardening Plan §4.2: <see cref="DbUpdateException"/> is unwrapped
/// using only the safe surface area (the inner SqlException's known numbers
/// for unique-constraint and FK violations on SQL Server) so we surface 409 /
/// 400 instead of an opaque 500. Anything else still 500s.
/// </para>
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    // SQL Server error numbers — kept narrow on purpose so the mapping only
    // fires on the violations we know how to surface. Any other DB failure
    // continues to bubble as a 500.
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlForeignKeyViolation = 547;

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
        // P4.1 — Severity downgrade. Client-originated (4xx) exceptions log at
        // Warning so they no longer flood Error telemetry / alerts. Server
        // exceptions (the fall-through 5xx branch) still log at Error.
        if (IsClientOriginated(exception))
        {
            _logger.LogWarning(
                exception,
                "Client-originated exception {ExceptionType}: {Message}",
                exception.GetType().Name,
                exception.Message);
        }
        else
        {
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
        }

        // Map each known exception to (status, localized-title-key, RFC type,
        // localized-detail-fallback-key). Detail-fallback is used when the
        // exception itself does not carry a known localization key — keeps
        // mixed-language responses out of the wire while staying backwards
        // compatible with literal messages still in flight from teammate code.
        var (statusCode, titleKey, type, fallbackDetailKey) = exception switch
        {
            ValidationException   => ((int)HttpStatusCode.BadRequest,         LocalizedKeys.Infrastructure.ValidationError,     "https://tools.ietf.org/html/rfc7231#section-6.5.1", LocalizedKeys.Infrastructure.ValidationError),
            UnauthorizedException => ((int)HttpStatusCode.Unauthorized,       LocalizedKeys.Auth.Unauthorized,                  "https://tools.ietf.org/html/rfc7235#section-3.1",  LocalizedKeys.Auth.Unauthorized),
            ForbiddenException    => ((int)HttpStatusCode.Forbidden,          LocalizedKeys.Permissions.Forbidden,              "https://tools.ietf.org/html/rfc7231#section-6.5.3", LocalizedKeys.Permissions.Forbidden),
            NotFoundException     => ((int)HttpStatusCode.NotFound,           LocalizedKeys.Infrastructure.NotFound,            "https://tools.ietf.org/html/rfc7231#section-6.5.4", LocalizedKeys.Infrastructure.NotFound),
            ConflictException     => ((int)HttpStatusCode.Conflict,           LocalizedKeys.Infrastructure.Conflict,            "https://tools.ietf.org/html/rfc7231#section-6.5.8", LocalizedKeys.Infrastructure.Conflict),
            DbUpdateConcurrencyException => ((int)HttpStatusCode.Conflict,    LocalizedKeys.Infrastructure.Conflict,            "https://tools.ietf.org/html/rfc7231#section-6.5.8", LocalizedKeys.Infrastructure.Conflict),
            DbUpdateException dbEx when IsUniqueViolation(dbEx) => ((int)HttpStatusCode.Conflict,    LocalizedKeys.Infrastructure.Conflict,           "https://tools.ietf.org/html/rfc7231#section-6.5.8", LocalizedKeys.Infrastructure.DuplicateKey),
            DbUpdateException dbEx when IsForeignKeyViolation(dbEx) => ((int)HttpStatusCode.BadRequest, LocalizedKeys.Infrastructure.ValidationError, "https://tools.ietf.org/html/rfc7231#section-6.5.1", LocalizedKeys.Infrastructure.ForeignKeyViolation),
            _                     => ((int)HttpStatusCode.InternalServerError, LocalizedKeys.Infrastructure.ServerError,        "https://tools.ietf.org/html/rfc7231#section-6.6.1", LocalizedKeys.Infrastructure.ServerError)
        };

        // Resolve the localization service from the request's scope — the handler
        // itself is registered as a singleton (AddExceptionHandler default), so
        // constructor-injecting a scoped service would create a captive dependency.
        var localization = httpContext.RequestServices.GetService<ILocalizationService>();
        var title = localization?.GetString(titleKey) ?? titleKey;
        var detail = ResolveDetail(localization, exception.Message, fallbackDetailKey);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        if (exception is ValidationException validationException)
        {
            // Per §2.4 — every error message in the array is also resolved against
            // the localization catalogue when it is a known key. Literal messages
            // (still flowing in from teammate-owned validators) pass through
            // untouched so behaviour stays backwards-compatible.
            problemDetails.Extensions["errors"] = LocalizeErrors(localization, validationException.Errors);
        }

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static bool IsClientOriginated(Exception exception) => exception is
        ValidationException or
        NotFoundException or
        ForbiddenException or
        UnauthorizedException or
        ConflictException;

    private static bool IsUniqueViolation(DbUpdateException dbEx)
    {
        var inner = dbEx.InnerException;
        // Microsoft.Data.SqlClient.SqlException carries a Number; check via
        // reflection to keep this assembly free of a hard provider reference.
        if (inner is null) return false;
        var numberProp = inner.GetType().GetProperty("Number");
        if (numberProp is null) return false;
        var number = (int?)numberProp.GetValue(inner);
        return number == SqlUniqueIndexViolation || number == SqlUniqueConstraintViolation;
    }

    private static bool IsForeignKeyViolation(DbUpdateException dbEx)
    {
        var inner = dbEx.InnerException;
        if (inner is null) return false;
        var numberProp = inner.GetType().GetProperty("Number");
        if (numberProp is null) return false;
        var number = (int?)numberProp.GetValue(inner);
        return number == SqlForeignKeyViolation;
    }

    private static string ResolveDetail(ILocalizationService? localization, string? message, string fallbackKey)
    {
        if (localization is null) return message ?? string.Empty;

        // If the exception message itself is a known localization key, resolve it.
        if (!string.IsNullOrEmpty(message) && localization.ContainsKey(message))
        {
            return localization.GetString(message);
        }

        // Empty / null message → emit the localized fallback. Otherwise pass the
        // literal through so callers still see context-specific details.
        if (string.IsNullOrWhiteSpace(message))
        {
            return localization.GetString(fallbackKey);
        }

        return message;
    }

    private static IDictionary<string, string[]> LocalizeErrors(
        ILocalizationService? localization,
        IReadOnlyDictionary<string, string[]> errors)
    {
        if (localization is null) return errors.ToDictionary(kv => kv.Key, kv => kv.Value);

        var localized = new Dictionary<string, string[]>(errors.Count);
        foreach (var (property, messages) in errors)
        {
            localized[property] = messages.Select(m => localization.ContainsKey(m) ? localization.GetString(m) : m).ToArray();
        }
        return localized;
    }
}
