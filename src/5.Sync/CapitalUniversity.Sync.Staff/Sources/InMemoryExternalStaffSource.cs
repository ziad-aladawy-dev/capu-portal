using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Sources;

/// <summary>
/// Deterministic in-memory simulator. Generates <see cref="TotalStaff"/> rows
/// matching the field set of Core <c>Identity.Staff</c>. Two rows (#5 and #15)
/// ship with empty emails so the validator drops them and warning aggregation
/// can be observed.
/// </summary>
public sealed class InMemoryExternalStaffSource : IExternalStaffSource
{
    public const int TotalStaff = 20;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new DateTimeOffset(2026, 02, 01, 00, 00, 00, TimeSpan.Zero);

    private static readonly (string Role, string JobTitle)[] RoleCycle =
    {
        ("instructor", "Lecturer"),
        ("instructor", "Senior Lecturer"),
        ("admin",      "Department Coordinator"),
        ("instructor", "Assistant Professor"),
        ("instructor", "Associate Professor")
    };

    private readonly IReadOnlyList<ExternalStaff> _store;

    public InMemoryExternalStaffSource()
    {
        var list = new List<ExternalStaff>(TotalStaff);
        for (var i = 1; i <= TotalStaff; i++)
        {
            var hasInvalidEmail = i == 5 || i == 15;
            var role = RoleCycle[i % RoleCycle.Length];
            list.Add(new ExternalStaff
            {
                ExternalStaffId = $"EXT-T-{i:D4}",
                EmployeeCode = $"EMP-{2000 + i}",
                Name = $"Staff Member {i}",
                NationalId = $"NID-T-{i:D10}",
                BirthDate = new DateTime(1975 + (i % 20), ((i - 1) % 12) + 1, ((i - 1) % 28) + 1),
                PhoneNumber = $"+202{(i * 54321 % 1_000_000_000):D9}",
                Email = hasInvalidEmail ? string.Empty : $"staff{i:D4}@university.test",
                Role = role.Role,
                JobTitle = role.JobTitle,
                IsActive = true,
                ExternalUpdatedAt = BaselineUpdatedAt.AddMinutes(i),
                ExternalVersion = 1
            });
        }
        _store = list;
    }

    public async IAsyncEnumerable<ExternalStaff> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var staff in _store)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || staff.ExternalUpdatedAt > sinceExclusive.Value)
            {
                yield return staff;
                await Task.Yield();
            }
        }
    }
}
