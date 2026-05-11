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
    void SetAuthorizationSource(Guid? sourceId, SourceType sourceType);
}
