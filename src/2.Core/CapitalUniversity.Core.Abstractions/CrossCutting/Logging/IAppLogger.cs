using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Logging;

public interface IAppLogger
{
    Task LogInfoAsync(
        string message,
        string source,
        HttpContext? context = null,
        Dictionary<string, object>? metadata = null);

    Task LogWarningAsync(
        string message,
        string source,
        HttpContext? context = null,
        Dictionary<string, object>? metadata = null);

    Task LogErrorAsync(
        string message,
        Exception exception,
        string source,
        HttpContext? context = null,
        Dictionary<string, object>? metadata = null);

    Task LogCriticalAsync(
        string message,
        Exception exception,
        string source,
        HttpContext? context = null,
        Dictionary<string, object>? metadata = null);
}
