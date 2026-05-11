namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface ITokenService
{
    string GenerateToken(IUserCredential user);
}