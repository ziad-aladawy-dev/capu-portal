namespace CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;

public class LoginRequestDto
{
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
