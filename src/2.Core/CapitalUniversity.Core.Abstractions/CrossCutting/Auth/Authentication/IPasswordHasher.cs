namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authentication;

public interface IPasswordHasher
{
    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
    string HashPassword(string password);
}