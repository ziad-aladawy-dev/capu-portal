namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;

public class PermissionDto
{
    public string Module { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
