using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.Logging;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogger _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, IAppLogger logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var source = context.Request.Path.HasValue ? context.Request.Path.Value : "GlobalExceptionMiddleware";

        await _logger.LogErrorAsync(
            "An unhandled exception occurred during the request.",
            exception,
            source,
            context);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An unexpected error occurred. Please try again later.",
            Detailed = exception.Message // For simplicity, we return the error message. Typically omitted in production.
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(jsonResponse);
    }
}
