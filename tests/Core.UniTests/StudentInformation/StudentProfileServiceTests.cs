using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.StudentInformation.DTOs;
using CapitalUniversity.Core.Application.StudentInformation;
using CapitalUniversity.Core.Application.StudentInformation.Validators;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.StudentInformation;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.StudentInformation;

/// <summary>
/// StudentProfileService contract: upsert keyed by (StudentId, Category,
/// CustomCategoryKey); JSON validity enforced at write; verification
/// stamps cleared on data edit; sensitive records get shorter cache TTL
/// (verified by invalidation behaviour, not internal TTL reads).
/// </summary>
public class StudentProfileServiceTests
{
    private sealed class StubCache : ICacheService
    {
        public int SetCalls;
        public int RemoveCalls;
        private readonly Dictionary<string, object?> _store = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken c = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken c = default)
        { SetCalls++; _store[key] = value; return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken c = default)
        { RemoveCalls++; _store.Remove(key); return Task.CompletedTask; }
    }

    private static (StudentProfileService Service, Mock<IStudentProfileRecordRepository> Repo, Mock<IUnitOfWork> Uow, StubCache Cache) Build()
    {
        var repo = new Mock<IStudentProfileRecordRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new StubCache();
        var scope = new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.IEffectiveScope>();
        scope.Setup(s => s.CanAccessStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        scope.Setup(s => s.CanAccessStructureNodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return (new StudentProfileService(uow.Object, repo.Object, new UpsertStudentProfileRecordValidator(), new VerifyStudentProfileRecordValidator(), cache, scope.Object),
                repo, uow, cache);
    }

    [Fact]
    public async Task Upsert_NewRecord_PersistsWithCanonicalCustomKey()
    {
        var (sut, repo, uow, _) = Build();
        var studentId = Guid.NewGuid();
        repo.Setup(r => r.GetForStudentCategoryAsync(studentId, StudentProfileCategory.EmergencyContact, "", default))
            .ReturnsAsync((StudentProfileRecord?)null);
        StudentProfileRecord? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StudentProfileRecord>(), default))
            .Callback<StudentProfileRecord, CancellationToken>((r, _) => captured = r);

        var id = await sut.UpsertAsync(studentId, new UpsertStudentProfileRecordRequest
        {
            Category = StudentProfileCategory.EmergencyContact,
            SchemaVersion = 1,
            DataJson = "{\"phone\":\"+201xxxxxxxxx\"}",
        });

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.StudentId.Should().Be(studentId);
        captured.CustomCategoryKey.Should().BeEmpty();
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Upsert_ExistingRecord_UpdatesAndClearsVerification()
    {
        var (sut, repo, _, cache) = Build();
        var studentId = Guid.NewGuid();
        var existing = new StudentProfileRecord
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Category = StudentProfileCategory.EmergencyContact,
            SchemaVersion = 1,
            DataJson = "{\"old\":true}",
            VerifiedBy = Guid.NewGuid(),
            VerifiedAt = DateTime.UtcNow,
        };
        repo.Setup(r => r.GetForStudentCategoryAsync(studentId, StudentProfileCategory.EmergencyContact, "", default))
            .ReturnsAsync(existing);

        var id = await sut.UpsertAsync(studentId, new UpsertStudentProfileRecordRequest
        {
            Category = StudentProfileCategory.EmergencyContact,
            SchemaVersion = 2,
            DataJson = "{\"new\":true}",
        });

        id.Should().Be(existing.Id);
        existing.SchemaVersion.Should().Be(2);
        existing.VerifiedBy.Should().BeNull("data edits must re-require verification");
        existing.VerifiedAt.Should().BeNull();
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Upsert_InvalidJson_ThrowsValidation()
    {
        var (sut, _, _, _) = Build();
        var act = () => sut.UpsertAsync(Guid.NewGuid(), new UpsertStudentProfileRecordRequest
        {
            Category = StudentProfileCategory.EmergencyContact,
            SchemaVersion = 1,
            DataJson = "not json {",
        });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Upsert_CustomCategoryMissingKey_ThrowsValidation()
    {
        var (sut, _, _, _) = Build();
        var act = () => sut.UpsertAsync(Guid.NewGuid(), new UpsertStudentProfileRecordRequest
        {
            Category = StudentProfileCategory.Custom,
            CustomCategoryKey = "",
            SchemaVersion = 1,
            DataJson = "{}",
        });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Verify_HappyPath_StampsAndInvalidatesCache()
    {
        var (sut, repo, _, cache) = Build();
        var record = new StudentProfileRecord
        {
            Id = Guid.NewGuid(), StudentId = Guid.NewGuid(),
            Category = StudentProfileCategory.MilitaryInformation, IsSensitive = true,
            SchemaVersion = 1, DataJson = "{}",
        };
        repo.Setup(r => r.GetByIdAsync(record.Id, default)).ReturnsAsync(record);

        var verifier = Guid.NewGuid();
        await sut.VerifyAsync(record.Id, new VerifyStudentProfileRecordRequest { VerifiedBy = verifier });

        record.VerifiedBy.Should().Be(verifier);
        record.VerifiedAt.Should().NotBeNull();
        cache.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Verify_UnknownRecord_ThrowsNotFound()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((StudentProfileRecord?)null);

        var act = () => sut.VerifyAsync(Guid.NewGuid(), new VerifyStudentProfileRecordRequest { VerifiedBy = Guid.NewGuid() });
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetById_CacheRoundTrip_HitsRepoOnce()
    {
        var (sut, repo, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync(new StudentProfileRecord
        {
            Id = id, StudentId = Guid.NewGuid(), Category = StudentProfileCategory.EmergencyContact,
            SchemaVersion = 1, DataJson = "{}",
        });

        await sut.GetByIdAsync(id);
        await sut.GetByIdAsync(id);

        repo.Verify(r => r.GetByIdAsync(id, default), Times.Once);
    }
}
