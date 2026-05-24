using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

/// <summary>
/// H11 — Verifies <see cref="SessionVersionService.IncrementVersionAsync"/>
/// returns the post-bump value and that the underlying row reflects the
/// increment after each call. The InMemory provider used by tests falls
/// back to the ORM read-modify-write path inside the service, so the test
/// also indirectly covers the non-relational branch. The atomic-SQL path
/// on relational providers is exercised in production; testing the actual
/// lost-update race deterministically needs a real SQL Server (deferred).
/// </summary>
public class SessionVersionAtomicIncrementTests
{
    private static CoreDbContext NewDb() => new(new DbContextOptionsBuilder<CoreDbContext>()
        .UseInMemoryDatabase("SessVerH11_" + Guid.NewGuid())
        .Options);

    [Fact]
    public async Task IncrementVersionAsync_OnStaff_BumpsAndReturnsNewValue()
    {
        using var db = NewDb();
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            EmployeeCode = "S-1",
            Email = "s1@u.edu",
            Name = "{\"ar\":\"S\",\"en\":\"S\"}",
            NationalId = "NID-1",
            PhoneNumber = "+1-1",
            PasswordHash = "x",
            JobTitle = "{\"ar\":\"J\",\"en\":\"J\"}",
            Role = "{\"ar\":\"R\",\"en\":\"R\"}",
            SessionVersion = 5,
        };
        db.Staffs.Add(staff);
        await db.SaveChangesAsync();

        var svc = new SessionVersionService(db);

        var first = await svc.IncrementVersionAsync(staff.Id);
        first.Should().Be(6);

        var second = await svc.IncrementVersionAsync(staff.Id);
        second.Should().Be(7);

        (await db.Staffs.AsNoTracking().FirstAsync(s => s.Id == staff.Id)).SessionVersion.Should().Be(7);
    }

    [Fact]
    public async Task IncrementVersionAsync_UnknownUser_ReturnsNull()
    {
        using var db = NewDb();
        var svc = new SessionVersionService(db);
        var result = await svc.IncrementVersionAsync(Guid.NewGuid());
        result.Should().BeNull();
    }
}
