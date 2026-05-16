using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

/// <summary>
/// Refresh-token contract: issue persists a hash (not the raw value), rotate is
/// single-use, expired/revoked tokens never rotate, and replay (presenting an
/// already-rotated token) revokes the full chain and bumps SessionVersion.
/// </summary>
public class RefreshTokenServiceTests
{
    private static CoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase("RT_" + Guid.NewGuid())
            .Options);

    private static IOptions<RefreshTokenSettings> Settings(int expiryDays = 30) =>
        Options.Create(new RefreshTokenSettings { ExpiryDays = expiryDays });

    private static Mock<IUserCredential> Cred(Guid id, string role = "Staff")
    {
        var m = new Mock<IUserCredential>();
        m.Setup(c => c.Id).Returns(id);
        m.Setup(c => c.Role).Returns(role);
        return m;
    }

    [Fact]
    public async Task Issue_StoresHashNotRaw_AndReturnsRawToCaller()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings());

        var userId = Guid.NewGuid();
        var issuance = await sut.IssueAsync(Cred(userId).Object);

        issuance.RawToken.Should().NotBeNullOrWhiteSpace();
        var stored = await db.Set<RefreshToken>().AsNoTracking().SingleAsync();
        stored.UserId.Should().Be(userId);
        stored.TokenHash.Should().NotBe(issuance.RawToken, "raw token must never land in the column");
        stored.TokenHash.Length.Should().Be(64, "SHA-256 hex digest");
        stored.RevokedAt.Should().BeNull();
        stored.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Rotate_HappyPath_RevokesOldAndReturnsNewPair()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var userId = Guid.NewGuid();
        var cred = Cred(userId).Object;
        resolver.Setup(r => r.ResolveByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cred);
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings());

        var first = await sut.IssueAsync(cred);
        var rotation = await sut.RotateAsync(first.RawToken);

        rotation.Should().NotBeNull();
        rotation!.RawToken.Should().NotBe(first.RawToken);
        rotation.Credential.Id.Should().Be(userId);

        var rows = await db.Set<RefreshToken>().AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].RevokedAt.Should().NotBeNull();
        rows[0].ReplacedByTokenHash.Should().Be(rows[1].TokenHash);
        rows[0].ReasonRevoked.Should().Be("rotated");
        rows[1].RevokedAt.Should().BeNull();

        sv.Verify(s => s.IncrementVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "happy-path rotation must not touch SessionVersion");
    }

    [Fact]
    public async Task Rotate_Expired_ReturnsNullWithoutSessionBump()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings(expiryDays: 30));

        var userId = Guid.NewGuid();
        var issuance = await sut.IssueAsync(Cred(userId).Object);

        // Forcibly expire the row.
        var row = await db.Set<RefreshToken>().SingleAsync();
        row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var rotation = await sut.RotateAsync(issuance.RawToken);
        rotation.Should().BeNull();
        sv.Verify(s => s.IncrementVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rotate_Unknown_ReturnsNull()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings());

        var rotation = await sut.RotateAsync("nope-not-a-real-token");
        rotation.Should().BeNull();
    }

    [Fact]
    public async Task Rotate_ReplayOfRotatedToken_RevokesChainAndBumpsSession()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var userId = Guid.NewGuid();
        var cred = Cred(userId).Object;
        resolver.Setup(r => r.ResolveByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cred);
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings());

        var first = await sut.IssueAsync(cred);
        var rotated = await sut.RotateAsync(first.RawToken);
        rotated.Should().NotBeNull();

        // Replay the original — it's revoked AND has a ReplacedByTokenHash. The service
        // must treat this as a compromise.
        var replay = await sut.RotateAsync(first.RawToken);
        replay.Should().BeNull();

        sv.Verify(s => s.IncrementVersionAsync(userId, It.IsAny<CancellationToken>()), Times.Once,
            "replay detection must bump SessionVersion");

        var rows = await db.Set<RefreshToken>().AsNoTracking().ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.RevokedAt != null,
            "both the rotated original and its successor land revoked after replay");
    }

    [Fact]
    public async Task Rotate_AlreadyRevokedButNotRotated_ReturnsNullWithoutChainSweep()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings());

        var userId = Guid.NewGuid();
        var issuance = await sut.IssueAsync(Cred(userId).Object);
        await sut.RevokeAllForUserAsync(userId, "logout");

        var rotation = await sut.RotateAsync(issuance.RawToken);
        rotation.Should().BeNull();

        sv.Verify(s => s.IncrementVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "a normal revocation isn't a compromise signal — only replay of a rotated token is");
    }

    [Fact]
    public async Task RevokeAllForUserAsync_OnlyTouchesActiveTokens()
    {
        using var db = NewDb();
        var resolver = new Mock<IUserCredentialResolver>();
        var sv = new Mock<ISessionVersionService>();
        var sut = new RefreshTokenService(db, resolver.Object, sv.Object, Settings());

        var userId = Guid.NewGuid();
        await sut.IssueAsync(Cred(userId).Object);
        await sut.IssueAsync(Cred(userId).Object);

        // Pre-revoke one manually with a distinct reason — the sweep should leave it alone.
        var preExisting = await db.Set<RefreshToken>().FirstAsync();
        preExisting.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        preExisting.ReasonRevoked = "manual";
        await db.SaveChangesAsync();

        await sut.RevokeAllForUserAsync(userId, "logout");

        var rows = await db.Set<RefreshToken>().AsNoTracking().ToListAsync();
        rows.Should().OnlyContain(r => r.RevokedAt != null);
        rows.Should().Contain(r => r.ReasonRevoked == "manual",
            "the pre-revoked row's reason must be preserved");
        rows.Should().Contain(r => r.ReasonRevoked == "logout");
    }
}
