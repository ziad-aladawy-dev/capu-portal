using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Repositories;
using CapitalUniversity.Sync.Student.Configuration;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Pull;
using CapitalUniversity.Sync.Student.Sources;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Verifies checkpoint-replay semantics for the Pull pipeline:
///   • A first run with no checkpoint streams every external record and advances
///     the cursor to the latest <c>ExternalUpdatedAt</c> observed.
///   • A second run loaded with that cursor skips already-processed records —
///     resume is honored, not a full re-pull.
///   • The extractor's safety-buffer ("clawback") window picks up records that
///     back-date slightly past the cursor — preventing the silent-skip class of
///     bug where retroactive upstream edits would otherwise be lost forever.
///   • The <see cref="SyncCheckpointStore"/> round-trips the cursor through the
///     audit DB, so a saved cursor on tick N is exactly what tick N+1 reads.
/// </summary>
public class CheckpointReplayTests
{
    private static readonly DateTimeOffset Baseline =
        new(2026, 01, 01, 00, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task FirstRun_NoCheckpoint_StreamsAllRecords_AdvancesCursor()
    {
        // Arrange
        var source = new InMemoryExternalStudentSource();
        var extractor = new StudentExtractor(source, BuildOptions(safetyBufferSeconds: 1));
        var context = BuildContext();

        // Act
        var pulled = await CollectAsync(extractor.ExtractAsync(context, checkpoint: null, CancellationToken.None));

        // Assert — all 50 students stream through; cursor is the latest stamp.
        pulled.Should().HaveCount(InMemoryExternalStudentSource.TotalStudents);
        var expectedMax = Baseline.AddMinutes(InMemoryExternalStudentSource.TotalStudents);
        extractor.CurrentCursor.Should().Be(expectedMax.ToString("O"));
    }

    [Fact]
    public async Task SecondRun_WithCheckpoint_SkipsAlreadyProcessedRecords()
    {
        // Arrange — pretend tick 1 already processed records 1..25.
        // Safety-buffer set to 0 so the cursor boundary is exact and the test
        // asserts the strict resume-from-cursor semantics (the clawback window
        // is covered separately by SafetyBuffer_RescuesBackdatedRecord).
        var source = new InMemoryExternalStudentSource();
        var extractor = new StudentExtractor(source, BuildOptions(safetyBufferSeconds: 0));
        var savedCursor = Baseline.AddMinutes(25);
        var checkpoint = new SyncCheckpoint
        {
            ModuleName = "students",
            LastSyncedAt = DateTimeOffset.UtcNow,
            Cursor = savedCursor.ToString("O")
        };

        // Act — tick 2 picks up where tick 1 stopped.
        var pulled = await CollectAsync(extractor.ExtractAsync(BuildContext(), checkpoint, CancellationToken.None));

        // Assert — records 26..50 only; the 1..25 prefix is not replayed.
        pulled.Should().HaveCount(InMemoryExternalStudentSource.TotalStudents - 25);
        pulled.Min(s => s.ExternalUpdatedAt).Should().Be(Baseline.AddMinutes(26));
        pulled.Max(s => s.ExternalUpdatedAt).Should().Be(Baseline.AddMinutes(50));

        var newCursor = DateTimeOffset.Parse(extractor.CurrentCursor!);
        newCursor.Should().Be(Baseline.AddMinutes(50), "cursor advances to the latest stamp seen on this tick");
    }

    [Fact]
    public async Task ReplayResume_ZeroNewRecords_LeavesCursorUntouched()
    {
        // Arrange — checkpoint is already at the latest stamp the source can offer.
        // Zero safety-buffer keeps the cursor boundary exact so 'nothing new'
        // is unambiguous (the clawback would otherwise re-emit the boundary row).
        var source = new InMemoryExternalStudentSource();
        var extractor = new StudentExtractor(source, BuildOptions(safetyBufferSeconds: 0));
        var checkpoint = new SyncCheckpoint
        {
            ModuleName = "students",
            LastSyncedAt = DateTimeOffset.UtcNow,
            Cursor = Baseline.AddMinutes(InMemoryExternalStudentSource.TotalStudents).ToString("O")
        };

        // Act
        var pulled = await CollectAsync(extractor.ExtractAsync(BuildContext(), checkpoint, CancellationToken.None));

        // Assert — nothing new past the cursor; cursor stays null because the
        // extractor never observed a record on this tick.
        pulled.Should().BeEmpty();
        extractor.CurrentCursor.Should().BeNull(
            "no records → no observation → nothing to advance; the prior persisted cursor wins");
    }

    [Fact]
    public async Task SafetyBuffer_RescuesBackdatedRecord()
    {
        // Arrange — checkpoint at minute 20; one record has a stamp at 19:30 due
        // to an upstream back-date (clock drift, retroactive edit, commit/stamp
        // ordering inversion). Without the safety buffer that record would be
        // skipped forever; with a 60-second clawback it is rescued.
        var backdatedStamp = Baseline.AddMinutes(20).AddSeconds(-30);
        var source = new BackdatedSource(stamp: backdatedStamp);
        var extractor = new StudentExtractor(source, BuildOptions(safetyBufferSeconds: 60));

        var checkpoint = new SyncCheckpoint
        {
            ModuleName = "students",
            LastSyncedAt = DateTimeOffset.UtcNow,
            Cursor = Baseline.AddMinutes(20).ToString("O")
        };

        // Act
        var pulled = await CollectAsync(extractor.ExtractAsync(BuildContext(), checkpoint, CancellationToken.None));

        // Assert — the back-dated record is included.
        pulled.Should().ContainSingle(s => s.ExternalStudentId == "EXT-BACKDATED")
            .Which.ExternalUpdatedAt.Should().Be(backdatedStamp);
    }

    [Fact]
    public async Task CheckpointStore_RoundTripsCursor_AcrossTicks()
    {
        // Arrange
        await using var db = new SyncDbContext(new DbContextOptionsBuilder<SyncDbContext>()
            .UseInMemoryDatabase("Checkpoint_" + Guid.NewGuid())
            .Options);
        var store = new SyncCheckpointStore(db);

        var cursorTick1 = Baseline.AddMinutes(15).ToString("O");

        // Act — tick 1 saves; tick 2 reads.
        await store.SaveAsync("students", new SyncCheckpoint
        {
            ModuleName = "students",
            LastSyncedAt = DateTimeOffset.UtcNow,
            Cursor = cursorTick1
        }, CancellationToken.None);

        var roundTripped = await store.GetAsync("students", CancellationToken.None);

        // Assert — exact cursor round-trip; modulename matches.
        roundTripped.Should().NotBeNull();
        roundTripped!.ModuleName.Should().Be("students");
        roundTripped.Cursor.Should().Be(cursorTick1);

        // Act 2 — tick 2 overwrites with a later cursor; tick 3 sees the latest.
        var cursorTick2 = Baseline.AddMinutes(40).ToString("O");
        await store.SaveAsync("students", new SyncCheckpoint
        {
            ModuleName = "students",
            LastSyncedAt = DateTimeOffset.UtcNow,
            Cursor = cursorTick2
        }, CancellationToken.None);

        var latest = await store.GetAsync("students", CancellationToken.None);
        latest!.Cursor.Should().Be(cursorTick2, "subsequent SaveAsync calls update the row in place — there is exactly one cursor per module");
    }

    [Fact]
    public async Task CheckpointStore_UnknownModule_ReturnsNull()
    {
        await using var db = new SyncDbContext(new DbContextOptionsBuilder<SyncDbContext>()
            .UseInMemoryDatabase("Checkpoint_" + Guid.NewGuid())
            .Options);
        var store = new SyncCheckpointStore(db);

        var missing = await store.GetAsync("never-persisted", CancellationToken.None);
        missing.Should().BeNull("a checkpoint-less module signals 'no resume point' so the next run starts from the beginning");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SyncContext BuildContext() => new()
    {
        ModuleName = "students",
        Direction = SyncDirection.Pull,
        Metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "checkpoint-test"
        }
    };

    private static IOptions<StudentSyncOptions> BuildOptions(int safetyBufferSeconds) =>
        Options.Create(new StudentSyncOptions
        {
            ConnectionString = "Server=none",
            BatchSize = 25,
            PushBatchSize = 25,
            ExtractorSafetyBufferSeconds = safetyBufferSeconds
        });

    private static async Task<List<ExternalStudent>> CollectAsync(IAsyncEnumerable<ExternalStudent> source)
    {
        var list = new List<ExternalStudent>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// Synthetic source that emits exactly one back-dated record at the given
    /// stamp. Used to prove the extractor's safety-buffer rescues records that
    /// arrive past the cursor but inside the clawback window.
    /// </summary>
    private sealed class BackdatedSource : IExternalStudentSource
    {
        private readonly DateTimeOffset _stamp;

        public BackdatedSource(DateTimeOffset stamp) => _stamp = stamp;

        public async IAsyncEnumerable<ExternalStudent> StreamChangesAsync(
            DateTimeOffset? sinceExclusive,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || _stamp > sinceExclusive.Value)
            {
                yield return new ExternalStudent
                {
                    ExternalStudentId = "EXT-BACKDATED",
                    StudentCode = "STU-BACKDATED",
                    Name = "Retro Edit",
                    NationalId = "NID-BACKDATED",
                    BirthDate = new DateTime(2001, 1, 1),
                    PhoneNumber = "+200000000000",
                    Email = "retro@university.test",
                    IsActive = true,
                    ExternalUpdatedAt = _stamp,
                    ExternalVersion = 1
                };
            }
            await Task.CompletedTask;
        }
    }
}
