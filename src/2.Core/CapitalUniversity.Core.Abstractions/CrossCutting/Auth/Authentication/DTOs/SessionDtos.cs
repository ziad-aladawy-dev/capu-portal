namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class RefreshTokenResponseDto
{
    public string Token { get; set; } = string.Empty;
}
