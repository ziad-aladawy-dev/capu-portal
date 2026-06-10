using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

public class PasswordResetServiceTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class CaptureSender : IPasswordResetSender
    {
        public string? LastLink { get; private set; }
        public int Calls { get; private set; }
        public Task SendAsync(string email, string name, string resetLink, CancellationToken cancellationToken = default)
        {
            LastLink = resetLink;
            Calls++;
            return Task.CompletedTask;
        }
    }

    private static (PasswordResetService svc, CoreDbContext db, CaptureSender sender,
        Mock<IUserCredentialResolver> resolver, Mock<ISessionVersionService> sessions,
        Mock<IRefreshTokenService> refresh) Build()
    {
        var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "hash:" + p);
        var sessions = new Mock<ISessionVersionService>();
        var refresh = new Mock<IRefreshTokenService>();
        var sender = new CaptureSender();
        var settings = Options.Create(new PasswordResetSettings
        {
            ExpiryMinutes = 30,
            ResetUrlBase = "http://localhost:5173/reset-password",
            MinPasswordLength = 8,
        });

        var svc = new PasswordResetService(db, resolver.Object, hasher.Object,
            sessions.Object, refresh.Object, sender, settings);
        return (svc, db, sender, resolver, sessions, refresh);
    }

    private static Mock<IUserCredential> Credential(Guid id)
    {
        var c = new Mock<IUserCredential>();
        c.Setup(x => x.Id).Returns(id);
        c.Setup(x => x.Role).Returns("Student");
        c.Setup(x => x.Name).Returns("Test Student");
        c.Setup(x => x.Email).Returns("student@uni.edu");
        return c;
    }

    private static string TokenFromLink(string link) =>
        Uri.UnescapeDataString(link.Split("token=")[1]);

    [Fact]
    public async Task RequestResetAsync_UnknownIdentifier_DoesNothing()
    {
        var (svc, db, sender, resolver, _, _) = Build();
        resolver.Setup(r => r.ResolveCredentialAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUserCredential?)null);

        await svc.RequestResetAsync("nope");

        Assert.Equal(0, sender.Calls);
        Assert.Empty(db.PasswordResetTokens);
    }

    [Fact]
    public async Task RequestResetAsync_KnownIdentifier_CreatesTokenAndSends()
    {
        var (svc, db, sender, resolver, _, _) = Build();
        var userId = Guid.NewGuid();
        resolver.Setup(r => r.ResolveCredentialAsync("nid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential(userId).Object);

        await svc.RequestResetAsync("nid");

        Assert.Equal(1, sender.Calls);
        var token = Assert.Single(db.PasswordResetTokens);
        Assert.Equal(userId, token.UserId);
        Assert.Null(token.ConsumedAt);
        Assert.Contains("token=", sender.LastLink);
    }

    [Fact]
    public async Task ResetAsync_ValidToken_SetsPasswordRevokesSessionsAndConsumesToken()
    {
        var (svc, db, sender, resolver, sessions, refresh) = Build();
        var userId = Guid.NewGuid();
        resolver.Setup(r => r.ResolveCredentialAsync("nid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential(userId).Object);
        resolver.Setup(r => r.SetPasswordAsync(userId, It.IsAny<string>(), It.IsAny<IPasswordHasher>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await svc.RequestResetAsync("nid");
        var raw = TokenFromLink(sender.LastLink!);

        var ok = await svc.ResetAsync(raw, "Newpass1!");

        Assert.True(ok);
        resolver.Verify(r => r.SetPasswordAsync(userId, "Newpass1!", It.IsAny<IPasswordHasher>(), It.IsAny<CancellationToken>()), Times.Once);
        refresh.Verify(r => r.RevokeAllForUserAsync(userId, "password-reset", It.IsAny<CancellationToken>()), Times.Once);
        sessions.Verify(s => s.IncrementVersionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(db.PasswordResetTokens.Single().ConsumedAt);
    }

    [Fact]
    public async Task ResetAsync_ReusedToken_ReturnsFalseSecondTime()
    {
        var (svc, db, sender, resolver, _, _) = Build();
        var userId = Guid.NewGuid();
        resolver.Setup(r => r.ResolveCredentialAsync("nid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential(userId).Object);
        resolver.Setup(r => r.SetPasswordAsync(userId, It.IsAny<string>(), It.IsAny<IPasswordHasher>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await svc.RequestResetAsync("nid");
        var raw = TokenFromLink(sender.LastLink!);

        Assert.True(await svc.ResetAsync(raw, "Newpass1!"));
        Assert.False(await svc.ResetAsync(raw, "Newpass2!")); // already consumed
    }

    [Fact]
    public async Task ResetAsync_UnknownToken_ReturnsFalse()
    {
        var (svc, _, _, _, _, _) = Build();
        Assert.False(await svc.ResetAsync("does-not-exist", "Newpass1!"));
    }

    [Fact]
    public async Task ResetAsync_ExpiredToken_ReturnsFalse()
    {
        var (svc, db, sender, resolver, _, _) = Build();
        var userId = Guid.NewGuid();
        resolver.Setup(r => r.ResolveCredentialAsync("nid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential(userId).Object);

        await svc.RequestResetAsync("nid");
        var raw = TokenFromLink(sender.LastLink!);

        // Force-expire the stored token.
        var token = db.PasswordResetTokens.Single();
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        Assert.False(await svc.ResetAsync(raw, "Newpass1!"));
    }

    [Fact]
    public async Task ResetAsync_TooShortPassword_ReturnsFalse()
    {
        var (svc, db, sender, resolver, _, _) = Build();
        var userId = Guid.NewGuid();
        resolver.Setup(r => r.ResolveCredentialAsync("nid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Credential(userId).Object);

        await svc.RequestResetAsync("nid");
        var raw = TokenFromLink(sender.LastLink!);

        Assert.False(await svc.ResetAsync(raw, "short"));
    }
}
