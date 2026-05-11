namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IPasswordHasher
{
    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
    string HashPassword(string password);
}