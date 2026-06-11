using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

/// <summary>
/// H5 — server-side password complexity (spec 1.3): min 8 chars with upper,
/// lower, digit, and special. Mirrors the SPA rule so a bypassing API caller
/// cannot set a weak password via change/reset.
/// </summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Aa1!aaaa")]   // exactly 8, all classes
    [InlineData("Str0ng#Pass")]
    [InlineData("Zx9$qwerty")]
    public void Compliant_Passwords_Pass(string password) =>
        PasswordPolicy.IsCompliant(password).Should().BeTrue();

    [Theory]
    [InlineData(null)]            // null
    [InlineData("")]             // empty
    [InlineData("Aa1!aa")]       // too short (6)
    [InlineData("aaaa1111!")]    // no uppercase
    [InlineData("AAAA1111!")]    // no lowercase
    [InlineData("Aaaaaaaa!")]    // no digit
    [InlineData("Aaaa1111")]     // no special
    public void NonCompliant_Passwords_Fail(string? password) =>
        PasswordPolicy.IsCompliant(password).Should().BeFalse();
}
