using Microsoft.Extensions.Logging;
using CapitalUniversity.Core.Abstractions.Cross-Cutting.Execution;
using CapitalUniversity.Core.Abstractions.Cross-Cutting.Audit;


namespace CapitalUniversity.Core.Application.Cross-Cutting.Audit;

public class SerilogLoggerService : ILoggerService
{
    private readonly ILogger<SerilogLoggerService> _logger;
    private readonly IExecutionContext _executionContext;

    public SerilogLoggerService(ILogger<SerilogLoggerService> logger, IExecutionContext executionContext)
    {
        _logger = logger;
        _executionContext = executionContext;
    }

    public void LogInformation(string message, string resource = "System")
    {
        _logger.LogInformation("{Message} [Resource: {Resource}, UserId: {UserId}, ActiveRoleId: {ActiveRoleId}, RequestId: {RequestId}, Operation: {Operation}]",
            message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);
    }

    public void LogWarning(string message, string resource = "System")
    {
        _logger.LogWarning("{Message} [Resource: {Resource}, UserId: {UserId}, ActiveRoleId: {ActiveRoleId}, RequestId: {RequestId}, Operation: {Operation}]",
            message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);
    }

    public void LogError(string message, Exception? exception = null, string resource = "System")
    {
        if (exception != null)
            _logger.LogError(exception, "{Message} [Resource: {Resource}, UserId: {UserId}, ActiveRoleId: {ActiveRoleId}, RequestId: {RequestId}, Operation: {Operation}]",
                message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);
        else
            _logger.LogError("{Message} [Resource: {Resource}, UserId: {UserId}, ActiveRoleId: {ActiveRoleId}, RequestId: {RequestId}, Operation: {Operation}]",
                message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);
    }
}
