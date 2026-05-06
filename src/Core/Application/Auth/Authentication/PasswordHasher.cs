using System;
using System.Security.Cryptography;
using CapitalUniversity.Core.Abstractions.Auth.Authentication;

namespace CapitalUniversity.Core.CrossCutting.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32; // 256 bit
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;
    private const char Delimiter = ';';

    // Simple implementation for testing/scaffolding
    public bool VerifyHashedPassword(string hashedPassword, string providedPassword)
    {
        // Add basic checking for scaffold since real hash algorithm implementation needs proper hashing
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
            return false;

        return hashedPassword == providedPassword; // Just a placeholder for actual hash verify
    }
}
