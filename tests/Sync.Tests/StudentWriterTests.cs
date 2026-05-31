using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Pull;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

public class StudentWriterTests
{
    private readonly StudentSyncDbContext _db;
    private readonly Mock<ILogger<StudentWriter>> _loggerMock = new();
    private readonly StudentWriter _sut;

    public StudentWriterTests()
    {
        var options = new DbContextOptionsBuilder<StudentSyncDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new StudentSyncDbContext(options);
        _sut = new StudentWriter(_db, _loggerMock.Object);
    }

    [Fact]
    public async Task UpsertBatchAsync_NewStudents_InsertsIntoDb()
    {
        // Arrange
        var batch = new[]
        {
            new StudentEntity { ExternalStudentId = "EXT1", FirstName = "John", LastName = "Doe" },
            new StudentEntity { ExternalStudentId = "EXT2", FirstName = "Jane", LastName = "Smith" }
        };

        // Act
        var result = await _sut.UpsertBatchAsync(batch, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        _db.Students.Count().Should().Be(2);
        var s1 = await _db.Students.FirstAsync(s => s.ExternalStudentId == "EXT1");
        s1.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task UpsertBatchAsync_ExistingStudents_UpdatesInPlace()
    {
        // Arrange
        _db.Students.Add(new StudentEntity { ExternalStudentId = "EXT1", FirstName = "Old", LastName = "Name" });
        await _db.SaveChangesAsync();

        var batch = new[]
        {
            new StudentEntity { ExternalStudentId = "EXT1", FirstName = "New", LastName = "Name" }
        };

        // Act
        var result = await _sut.UpsertBatchAsync(batch, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        _db.Students.Count().Should().Be(1);
        var s1 = await _db.Students.FirstAsync(s => s.ExternalStudentId == "EXT1");
        s1.FirstName.Should().Be("New");
    }

    [Fact]
    public async Task UpsertBatchAsync_MixedBatch_InsertsAndUpdates()
    {
        // Arrange
        _db.Students.Add(new StudentEntity { ExternalStudentId = "EXT1", FirstName = "Old", LastName = "Name" });
        await _db.SaveChangesAsync();

        var batch = new[]
        {
            new StudentEntity { ExternalStudentId = "EXT1", FirstName = "Updated", LastName = "Name" },
            new StudentEntity { ExternalStudentId = "EXT2", FirstName = "New", LastName = "Student" }
        };

        // Act
        var result = await _sut.UpsertBatchAsync(batch, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        _db.Students.Count().Should().Be(2);
    }
}