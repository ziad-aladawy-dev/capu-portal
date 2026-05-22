using System;
using CapitalUniversity.Core.Abstractions;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Execution;

public interface IExecutionContext
{
    Guid UserId { get; }
    Guid ActiveRoleId { get; }
    string RequestId { get; }
    string Operation { get; }
    Guid? AuthorizationSourceId { get; }
    SourceType AuthorizationSourceType { get; }

    /// <summary>
    /// True when the current logical flow is an internal system process
    /// (e.g. outbox dispatcher, background worker) rather than a user-driven
    /// request. Used by authorization guards to allow trusted operations
    /// when no HttpContext / Principal is present.
    /// </summary>
    bool IsSystem { get; }

    void SetAuthorizationSource(Guid? sourceId, SourceType sourceType);

    /// <summary>
    /// Internal-only: sets the <see cref="IsSystem"/> flag for the current
    /// async flow. Callers should use <c>SystemExecutionScope</c> instead
    /// of calling this directly.
    /// </summary>
    void SetSystemMode(bool isSystem);
}
