namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authentication;

public interface ITokenService
{
    string GenerateToken(IUserCredential user);
}