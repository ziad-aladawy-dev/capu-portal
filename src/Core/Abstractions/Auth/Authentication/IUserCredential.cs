using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.Auth.Authentication;

public interface IUserCredential
{
    string Identifier { get; } // Maps to NationalId
    string PasswordHash { get; }
    DateTime PasswordExpiry { get; }
    // TODO: Add Role or Module identity properties if needed by TokenService
}