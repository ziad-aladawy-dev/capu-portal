namespace CapitalUniversity.Core.Abstractions.Auth.Authentication;

public interface IPasswordHasher
{
    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
    // TODO: Add HashPassword abstraction if needed
}