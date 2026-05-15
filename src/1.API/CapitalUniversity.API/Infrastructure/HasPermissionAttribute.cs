using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace CapitalUniversity.API.Infrastructure;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"{PermissionIdentity.Prefix}{PermissionIdentity.Parse(permission)}";
    }
}
