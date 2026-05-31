using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Sources;

/// <summary>
/// Deterministic in-memory external source used until a real adapter replaces it.
/// Generates 50 students; two of them (#10 and #20) have empty emails so the
/// validator drops them and warning aggregation can be observed.
/// </summary>
public sealed class InMemoryExternalStudentSource : IExternalStudentSource
{
    public const int TotalStudents = 50;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new DateTimeOffset(2026, 01, 01, 00, 00, 00, TimeSpan.Zero);

    private readonly IReadOnlyList<ExternalStudent> _store;

    public InMemoryExternalStudentSource()
    {
        var list = new List<ExternalStudent>(TotalStudents);
        for (var i = 1; i <= TotalStudents; i++)
        {
            var hasInvalidEmail = i == 10 || i == 20;
            list.Add(new ExternalStudent
            {
                ExternalStudentId = $"EXT-S-{i:D4}",
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Email = hasInvalidEmail ? string.Empty : $"student{i:D4}@university.test",
                ExternalUpdatedAt = BaselineUpdatedAt.AddMinutes(i),
                ExternalVersion = 1
            });
        }
        _store = list;
    }

    public async IAsyncEnumerable<ExternalStudent> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var student in _store)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || student.ExternalUpdatedAt > sinceExclusive.Value)
            {
                yield return student;
                await Task.Yield();
            }
        }
    }
}