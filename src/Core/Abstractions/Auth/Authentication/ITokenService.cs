namespace CapitalUniversity.Core.Abstractions.Auth.Authentication;

public interface ITokenService
{
    string GenerateToken(IUserCredential user);
}