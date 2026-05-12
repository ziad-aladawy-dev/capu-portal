using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Audit;

public interface ILoggerService
{
    void LogInformation(string message, string resource = "System");
    void LogWarning(string message, string resource = "System");
    void LogError(string message, Exception? exception = null, string resource = "System");
}
