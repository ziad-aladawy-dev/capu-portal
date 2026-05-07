namespace CapitalUniversity.Core.Abstractions.Auth.Authentication.DTOs;

public class LoginRequestDto
{
    public string Identifier { get; set; }
    public string Password { get; set; }
}
