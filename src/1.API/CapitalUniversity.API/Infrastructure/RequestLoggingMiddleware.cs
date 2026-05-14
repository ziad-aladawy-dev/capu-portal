using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.API.Infrastructure;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;
        var traceId = context.TraceIdentifier;

        _logger.LogInformation("HTTP {Method} {Path} started. TraceId: {TraceId}", 
            requestMethod, requestPath, traceId);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            
            _logger.LogInformation("HTTP {Method} {Path} finished with {StatusCode} in {Elapsed}ms. TraceId: {TraceId}", 
                requestMethod, requestPath, statusCode, stopwatch.ElapsedMilliseconds, traceId);
        }
    }
}
