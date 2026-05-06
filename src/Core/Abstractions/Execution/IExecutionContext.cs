using System;
using CapitalUniversity.Core.Abstractions.Auth.Authorization;

namespace CapitalUniversity.Core.Abstractions.Execution;

public interface IExecutionContext
{
    Guid UserId { get; }
    Guid ActiveRoleId { get; }
    string RequestId { get; }
    string Operation { get; }
    Guid? AuthorizationSourceId { get; }
    SourceType AuthorizationSourceType { get; }
    void SetAuthorizationSource(Guid? sourceId, SourceType sourceType);
}
