using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Sources;

/// <summary>
/// Deterministic 20-staff in-memory simulator. Two rows (#5 and #15) ship with
/// empty emails so the validator drops them and warning aggregation can be
/// observed in the Phase 7 verification ticks.
/// </summary>
public sealed class InMemoryExternalStaffSource : IExternalStaffSource
{
    public const int TotalStaff = 20;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new DateTimeOffset(2026, 02, 01, 00, 00, 00, TimeSpan.Zero);

    private static readonly string[] Departments =
    {
        "Mathematics", "Physics", "Chemistry", "Biology", "Computing",
        "Economics", "History", "Literature", "Philosophy", "Engineering"
    };

    private readonly IReadOnlyList<ExternalStaff> _store;

    public InMemoryExternalStaffSource()
    {
        var list = new List<ExternalStaff>(TotalStaff);
        for (var i = 1; i <= TotalStaff; i++)
        {
            var hasInvalidEmail = i == 5 || i == 15;
            list.Add(new ExternalStaff
            {
                ExternalStaffId = $"EXT-T-{i:D4}",
                FirstName = $"Staff{i}",
                LastName = $"Surname{i}",
                Email = hasInvalidEmail ? string.Empty : $"staff{i:D4}@university.test",
                Department = Departments[i % Departments.Length],
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