using Microsoft.Extensions.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;


namespace CapitalUniversity.Core.Application.CrossCutting.Audit;

public class SerilogLoggerService : ILoggerService
{
    private const string LogTemplate =
        "{Message} [Resource: {Resource}, UserId: {UserId}, ActiveRoleId: {ActiveRoleId}, RequestId: {RequestId}, Operation: {Operation}]";

    private readonly ILogger<SerilogLoggerService> _logger;
    private readonly IExecutionContext _executionContext;

    public SerilogLoggerService(ILogger<SerilogLoggerService> logger, IExecutionContext executionContext)
    {
        _logger = logger;
        _executionContext = executionContext;
    }

    public void LogInformation(string message, string resource = "System") =>
        _logger.LogInformation(LogTemplate,
            message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);

    public void LogWarning(string message, string resource = "System") =>
        _logger.LogWarning(LogTemplate,
            message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);

    public void LogError(string message, Exception? exception = null, string resource = "System")
    {
        if (exception != null)
            _logger.LogError(exception, LogTemplate,
                message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);
        else
            _logger.LogError(LogTemplate,
                message, resource, _executionContext.UserId, _executionContext.ActiveRoleId, _executionContext.RequestId, _executionContext.Operation);
    }
}
