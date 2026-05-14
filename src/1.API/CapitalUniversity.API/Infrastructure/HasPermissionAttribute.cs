using Microsoft.AspNetCore.Authorization;

namespace CapitalUniversity.API.Infrastructure;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string module, string resource, string action)
    {
        Policy = $"Permission:{module}:{resource}:{action}";
    }
}
