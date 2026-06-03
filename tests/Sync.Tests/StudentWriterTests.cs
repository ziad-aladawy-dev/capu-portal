using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Sync.Student.Pull;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

// `Student` is a namespace under CapitalUniversity.Sync.* — shadows Core's
// Identity.Student type when this assembly's namespace tree resolves the name.
// Alias to access the Core entity.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// The Student writer is a thin adapter over <see cref="ICoreWriteGateway"/>.
/// One fact under test: it forwards the batch with update-only options and a
/// merge delegate that touches only the columns sync owns. Everything else
/// (merge orchestration, external-wins, SaveChanges) is the gateway's job and
/// is exercised in gateway-level tests in the Core test suite.
/// </summary>
public class StudentWriterTests
{
    [Fact]
    public async Task UpsertBatchAsync_EmptyBatch_DoesNotCallGateway()
    {
        var gateway = new Mock<ICoreWriteGateway>(MockBehavior.Strict);
        var sut = new StudentWriter(gateway.Object, NullLogger<StudentWriter>.Instance);

        var result = await sut.UpsertBatchAsync(Array.Empty<CoreStudent>(), CancellationToken.None);

        result.Should().Be(0);
        gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpsertBatchAsync_ForwardsBatchToGateway_WithUpdateOnlyOptions()
    {
        // The writer is configured to never insert — Student.StructureNodeId is
        // out of sync scope per the platform plan. Verify the gateway gets the
        // right CoreUpsertOptions.
        var gateway = new Mock<ICoreWriteGateway>();
        CoreUpsertOptions? capturedOptions = null;
        gateway
            .Setup(g => g.UpsertAsync(
                It.IsAny<IReadOnlyList<CoreStudent>>(),
                It.IsAny<Action<CoreStudent, CoreStudent>>(),
                It.IsAny<CoreUpsertOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CoreStudent>, Action<CoreStudent, CoreStudent>, CoreUpsertOptions, CancellationToken>(
                (_, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(new CoreUpsertResult { Persisted = 1, SkippedNotFound = 0 });

        var sut = new StudentWriter(gateway.Object, NullLogger<StudentWriter>.Instance);
        var batch = new[] { BuildStudent("EXT-S-0001") };

        await sut.UpsertBatchAsync(batch, CancellationToken.None);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.AllowInsert.Should().BeFalse("Student requires a StructureNodeId sync cannot supply");
        capturedOptions.RespectExternalUpdatedAt.Should().BeTrue("external-wins guards against stale upstream replays");
    }

    [Fact]
    public async Task UpsertBatchAsync_MergeDelegate_CopiesSyncOwnedFieldsOnly()
    {
        // Capture the delegate the writer hands to the gateway and exercise it
        // against a pair of test entities. This pins the contract: sync touches
        // the data columns it owns and never the auth / structure / lifecycle
        // columns Core controls.
        Action<CoreStudent, CoreStudent>? captured = null;
        var gateway = new Mock<ICoreWriteGateway>();
        gateway
            .Setup(g => g.UpsertAsync(
                It.IsAny<IReadOnlyList<CoreStudent>>(),
                It.IsAny<Action<CoreStudent, CoreStudent>>(),
                It.IsAny<CoreUpsertOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CoreStudent>, Action<CoreStudent, CoreStudent>, CoreUpsertOptions, CancellationToken>(
                (_, apply, _, _) => captured = apply)
            .ReturnsAsync(new CoreUpsertResult { Persisted = 1 });

        var sut = new StudentWriter(gateway.Object, NullLogger<StudentWriter>.Instance);
        await sut.UpsertBatchAsync(new[] { BuildStudent("EXT-S-0001") }, CancellationToken.None);

        captured.Should().NotBeNull("the writer must hand the merge delegate to the gateway");

        var existing = new CoreStudent
        {
            Id = Guid.NewGuid(),
            StudentCode = "STU-ORIG",
            Name = "{\"ar\":\"اصلي\",\"en\":\"Original\"}",
            NationalId = "NID-ORIG",
            BirthDate = new DateTime(1999, 1, 1),
            PhoneNumber = "+200000000000",
            Email = "orig@test",
            // Auth + lifecycle fields the writer MUST NOT touch.
            PasswordHash = "DO-NOT-OVERWRITE",
            StructureNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SessionVersion = 42,
            IsActive = true
        };

        var incoming = new CoreStudent
        {
            StudentCode = "STU-NEW",
            Name = "{\"ar\":\"جديد\",\"en\":\"New\"}",
            NationalId = "NID-NEW",
            BirthDate = new DateTime(2001, 5, 5),
            PhoneNumber = "+201111111111",
            Email = "new@test",
            // Hostile values: if the writer copies these blindly, the test fails.
            PasswordHash = "ATTACKER-SUPPLIED",
            StructureNodeId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            SessionVersion = 0,
            IsActive = false
        };

        captured!(existing, incoming);

        // Sync-owned fields were copied.
        existing.StudentCode.Should().Be("STU-NEW");
        existing.Name.Should().Be("{\"ar\":\"جديد\",\"en\":\"New\"}");
        existing.NationalId.Should().Be("NID-NEW");
        existing.BirthDate.Should().Be(new DateTime(2001, 5, 5));
        existing.PhoneNumber.Should().Be("+201111111111");
        existing.Email.Should().Be("new@test");
        existing.IsActive.Should().BeFalse();

        // Untouched fields preserved.
        existing.PasswordHash.Should().Be("DO-NOT-OVERWRITE", "sync MUST NOT overwrite passwords");
        existing.StructureNodeId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "StructureNode is out of sync scope");
        existing.SessionVersion.Should().Be(42, "session lifecycle is Core's job");
    }

    [Fact]
    public async Task UpsertBatchAsync_ReturnsGatewayPersistedCount()
    {
        var gateway = new Mock<ICoreWriteGateway>();
        gateway
            .Setup(g => g.UpsertAsync(
                It.IsAny<IReadOnlyList<CoreStudent>>(),
                It.IsAny<Action<CoreStudent, CoreStudent>>(),
                It.IsAny<CoreUpsertOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CoreUpsertResult { Persisted = 17, SkippedNotFound = 3 });

        var sut = new StudentWriter(gateway.Object, NullLogger<StudentWriter>.Instance);
        var batch = new[] { BuildStudent("EXT-S-0001"), BuildStudent("EXT-S-0002") };

        var result = await sut.UpsertBatchAsync(batch, CancellationToken.None);

        result.Should().Be(17, "the pipeline measures progress by what the gateway actually persisted");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static CoreStudent BuildStudent(string externalId) => new()
    {
        ExternallySourced = new() { ExternalId = externalId },
        StudentCode = $"STU-{externalId}",
        Name = "{\"ar\":\"اسم\",\"en\":\"Name\"}",
        NationalId = $"NID-{externalId}",
        BirthDate = new DateTime(2001, 1, 1),
        PhoneNumber = "+200000000000",
        Email = $"{externalId.ToLowerInvariant()}@test",
        IsActive = true,
    };
}
