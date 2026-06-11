using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Application.Students;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.StudentInformation;

/// <summary>
/// Mutation-focused unit tests for <see cref="StudentService"/> (Core.Application).
/// Targets every guard branch, the student-code generator, the bilingual name
/// merge, and the projection's parent-walk (level → program → faculty).
/// </summary>
public class StudentServiceCoreTests
{
    private static (StudentService sut,
                    Mock<IStudentRepository> repo,
                    Mock<IStructureNodeRepository> structure,
                    Mock<IPasswordHasher> hasher,
                    Mock<ILocalizationService> loc) Build()
    {
        var repo = new Mock<IStudentRepository>();
        var structure = new Mock<IStructureNodeRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var loc = new Mock<ILocalizationService>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "HASH:" + p);
        // Echo the localized string back via the "en" extraction convention.
        loc.Setup(l => l.Get<string>(It.IsAny<string>())).Returns<string>(s => s);
        loc.Setup(l => l.GetLocalizedString(It.IsAny<string>())).Returns<string>(s => s);
        var sut = new StudentService(repo.Object, structure.Object, hasher.Object, loc.Object);
        return (sut, repo, structure, hasher, loc);
    }

    private static StructureNode Level(string name = "Level-1") =>
        new() { Id = Guid.NewGuid(), Name = name, Type = StructureNodeType.Level };

    private static CreateStudentRequest ValidCreate(Guid nodeId) => new()
    {
        StudentCode = "STU-2000",
        Password = "pw",
        ConfirmPassword = "pw",
        NameAr = "اسم",
        NameEn = "Name",
        NationalId = "NID-1",
        Email = "s@test.com",
        PhoneNumber = "+200",
        StructureNodeId = nodeId,
        BirthDate = new DateTime(2000, 1, 1),
    };

    // ── CreateAsync guards ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_DuplicateStudentCode_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.StudentCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(Guid.NewGuid()));

        await act.Should().ThrowAsync<Exception>().WithMessage("Student code already exists");
        repo.Verify(r => r.AddAsync(It.IsAny<Student>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.StudentCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(Guid.NewGuid()));

        await act.Should().ThrowAsync<Exception>().WithMessage("Email already exists");
    }

    [Fact]
    public async Task CreateAsync_DuplicateNationalId_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.NationalIdExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(Guid.NewGuid()));

        await act.Should().ThrowAsync<Exception>().WithMessage("National ID already exists");
    }

    [Fact]
    public async Task CreateAsync_PasswordMismatch_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        var req = ValidCreate(Guid.NewGuid());
        req.ConfirmPassword = "different";

        var act = () => sut.CreateAsync(req);

        await act.Should().ThrowAsync<Exception>().WithMessage("Passwords do not match");
    }

    [Fact]
    public async Task CreateAsync_StructureNodeMissing_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);

        var act = () => sut.CreateAsync(ValidCreate(Guid.NewGuid()));

        await act.Should().ThrowAsync<Exception>().WithMessage("Structure node not found");
    }

    [Fact]
    public async Task CreateAsync_StructureNodeNotLevel_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = new StructureNode { Id = Guid.NewGuid(), Type = StructureNodeType.Program };
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);

        var act = () => sut.CreateAsync(ValidCreate(node.Id));

        await act.Should().ThrowAsync<Exception>().WithMessage("Student must be assigned to level node");
    }

    [Fact]
    public async Task CreateAsync_Valid_PersistsHashedPasswordAndReturnsId()
    {
        var (sut, repo, structure, hasher, _) = Build();
        var node = Level();
        structure.Setup(s => s.GetByIdAsync(node.Id)).ReturnsAsync(node);
        Student? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => saved = s);

        var id = await sut.CreateAsync(ValidCreate(node.Id));

        saved.Should().NotBeNull();
        id.Should().Be(saved!.Id);
        saved.PasswordHash.Should().Be("HASH:pw");
        saved.IsActive.Should().BeTrue();
        saved.StructureNodeId.Should().Be(node.Id);
        saved.Email.Should().Be("s@test.com");
        repo.Verify(r => r.AddAsync(It.IsAny<Student>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NameEnBlank_FallsBackToArabic()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        structure.Setup(s => s.GetByIdAsync(node.Id)).ReturnsAsync(node);
        Student? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => saved = s);
        var req = ValidCreate(node.Id);
        req.NameAr = "عربي";
        req.NameEn = "   ";

        await sut.CreateAsync(req);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(saved!.Name)!;
        dict["ar"].Should().Be("عربي");
        dict["en"].Should().Be("عربي", "blank English name falls back to Arabic");
    }

    [Fact]
    public async Task CreateAsync_NameEnProvided_KeepsEnglish()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        structure.Setup(s => s.GetByIdAsync(node.Id)).ReturnsAsync(node);
        Student? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => saved = s);
        var req = ValidCreate(node.Id);
        req.NameAr = "عربي";
        req.NameEn = "English";

        await sut.CreateAsync(req);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(saved!.Name)!;
        dict["en"].Should().Be("English");
    }

    [Fact]
    public async Task CreateAsync_BlankCode_NoPriorCodes_GeneratesSeed()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        structure.Setup(s => s.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetLastStudentCodeAsync()).ReturnsAsync((string?)null);
        Student? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => saved = s);
        var req = ValidCreate(node.Id);
        req.StudentCode = "   ";

        await sut.CreateAsync(req);

        saved!.StudentCode.Should().Be("STU-1001");
    }

    [Fact]
    public async Task CreateAsync_BlankCode_WithPriorCode_IncrementsNumber()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        structure.Setup(s => s.GetByIdAsync(node.Id)).ReturnsAsync(node);
        repo.Setup(r => r.GetLastStudentCodeAsync()).ReturnsAsync("STU-1005");
        Student? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => saved = s);
        var req = ValidCreate(node.Id);
        req.StudentCode = "";

        await sut.CreateAsync(req);

        saved!.StudentCode.Should().Be("STU-1006");
    }

    // ── UpdateAsync ─────────────────────────────────────────────────────

    private static Student ExistingStudent(Guid nodeId) => new()
    {
        Id = Guid.NewGuid(),
        StudentCode = "STU-1",
        Name = JsonSerializer.Serialize(new Dictionary<string, string> { ["ar"] = "أ", ["en"] = "A" }),
        NationalId = "OLD-NID",
        Email = "old@test.com",
        StructureNodeId = nodeId,
        IsActive = true,
    };

    private static UpdateStudentRequest ValidUpdate(Guid nodeId) => new()
    {
        NameAr = "",
        NameEn = "",
        NationalId = "OLD-NID",
        Email = "old@test.com",
        PhoneNumber = "+201",
        StructureNodeId = nodeId,
        IsActive = true,
        BirthDate = new DateTime(2001, 2, 2),
    };

    [Fact]
    public async Task UpdateAsync_StudentMissing_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Student?)null);

        var act = () => sut.UpdateAsync(Guid.NewGuid(), ValidUpdate(Guid.NewGuid()));

        await act.Should().ThrowAsync<Exception>().WithMessage("Student not found");
    }

    [Fact]
    public async Task UpdateAsync_StructureNodeMissing_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(node.Id));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);

        var act = () => sut.UpdateAsync(Guid.NewGuid(), ValidUpdate(node.Id));

        await act.Should().ThrowAsync<Exception>().WithMessage("Structure node not found");
    }

    [Fact]
    public async Task UpdateAsync_StructureNodeNotLevel_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = new StructureNode { Id = Guid.NewGuid(), Type = StructureNodeType.Faculty };
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(node.Id));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);

        var act = () => sut.UpdateAsync(Guid.NewGuid(), ValidUpdate(node.Id));

        await act.Should().ThrowAsync<Exception>().WithMessage("Student must be assigned to a level");
    }

    [Fact]
    public async Task UpdateAsync_EmailChangedToExisting_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(node.Id));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);
        repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        var req = ValidUpdate(node.Id);
        req.Email = "new@test.com"; // differs from existing old@test.com

        var act = () => sut.UpdateAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<Exception>().WithMessage("Email already exists");
    }

    [Fact]
    public async Task UpdateAsync_EmailExistsButUnchanged_Succeeds()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(node.Id));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);
        repo.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        // Email stays old@test.com (same as existing) -> the "&& changed" guard must not fire.

        await sut.UpdateAsync(Guid.NewGuid(), ValidUpdate(node.Id));

        repo.Verify(r => r.UpdateAsync(It.IsAny<Student>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NationalIdChangedToExisting_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(node.Id));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);
        repo.Setup(r => r.NationalIdExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        var req = ValidUpdate(node.Id);
        req.NationalId = "CHANGED-NID";

        var act = () => sut.UpdateAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<Exception>().WithMessage("National ID already exists");
    }

    [Fact]
    public async Task UpdateAsync_PasswordMismatch_Throws()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(node.Id));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);
        var req = ValidUpdate(node.Id);
        req.Password = "a";
        req.ConfirmPassword = "b";

        var act = () => sut.UpdateAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<Exception>().WithMessage("Passwords do not match");
    }

    [Fact]
    public async Task UpdateAsync_PasswordSet_HashesAndApplies()
    {
        var (sut, repo, structure, hasher, _) = Build();
        var node = Level();
        var student = ExistingStudent(node.Id);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(student);
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);
        var req = ValidUpdate(node.Id);
        req.Password = "secret";
        req.ConfirmPassword = "secret";

        await sut.UpdateAsync(Guid.NewGuid(), req);

        student.PasswordHash.Should().Be("HASH:secret");
    }

    [Fact]
    public async Task UpdateAsync_MergesNameAndCopiesFields()
    {
        var (sut, repo, structure, _, _) = Build();
        var node = Level();
        var student = ExistingStudent(node.Id);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(student);
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(node);
        var req = ValidUpdate(node.Id);
        req.NameEn = "Updated";   // only en provided -> ar preserved
        req.PhoneNumber = "+209";
        req.IsActive = false;

        await sut.UpdateAsync(Guid.NewGuid(), req);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(student.Name)!;
        dict["en"].Should().Be("Updated");
        dict["ar"].Should().Be("أ", "Arabic name preserved when only English changes");
        student.PhoneNumber.Should().Be("+209");
        student.IsActive.Should().BeFalse();
        student.UpdatedAt.Should().NotBeNull();
    }

    // ── Delete / Toggle ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Missing_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var act = () => sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>().WithMessage("Student not found");
        repo.Verify(r => r.SoftDeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Exists_SoftDeletesAndSaves()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.ExistsAsync(id)).ReturnsAsync(true);

        await sut.DeleteAsync(id);

        repo.Verify(r => r.SoftDeleteAsync(id), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleStatusAsync_Missing_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var act = () => sut.ToggleStatusAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>().WithMessage("Student not found");
        repo.Verify(r => r.ToggleStatusAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ToggleStatusAsync_Exists_TogglesAndSaves()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.ExistsAsync(id)).ReturnsAsync(true);

        await sut.ToggleStatusAsync(id);

        repo.Verify(r => r.ToggleStatusAsync(id), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ── Reads / projection ──────────────────────────────────────────────

    private static Student StudentWithHierarchy(bool withProgram, bool withFaculty)
    {
        var faculty = new StructureNode { Id = Guid.NewGuid(), Name = "FacultyName", Type = StructureNodeType.Faculty };
        var program = new StructureNode { Id = Guid.NewGuid(), Name = "ProgramName", Type = StructureNodeType.Program, Parent = withFaculty ? faculty : null };
        var level = new StructureNode { Id = Guid.NewGuid(), Name = "LevelName", Type = StructureNodeType.Level, Parent = withProgram ? program : null };
        return new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "STU-9",
            Name = "StudentName",
            NationalId = "NID",
            Email = "x@test.com",
            StructureNodeId = level.Id,
            StructureNode = level,
            IsActive = true,
        };
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Student?)null);

        (await sut.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_FullHierarchy_MapsFacultyProgramLevel()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(StudentWithHierarchy(true, true));

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto.Should().NotBeNull();
        dto!.LevelName.Should().Be("LevelName");
        dto.ProgramName.Should().Be("ProgramName");
        dto.FacultyName.Should().Be("FacultyName");
        dto.StructureNodeName.Should().Be("LevelName");
    }

    [Fact]
    public async Task GetByIdAsync_NoProgramParent_LeavesProgramAndFacultyEmpty()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(StudentWithHierarchy(false, false));

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto!.ProgramName.Should().BeEmpty();
        dto.FacultyName.Should().BeEmpty();
        dto.LevelName.Should().Be("LevelName");
    }

    [Fact]
    public async Task GetByIdAsync_ProgramWithoutFaculty_FacultyEmptyProgramSet()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(StudentWithHierarchy(true, false));

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto!.ProgramName.Should().Be("ProgramName");
        dto.FacultyName.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_MapsEveryStudent()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student>
        {
            StudentWithHierarchy(true, true),
            StudentWithHierarchy(false, false),
        });

        var list = await sut.GetAllAsync();

        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_CopiesPagingMetadata()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<StudentQueryRequest>())).ReturnsAsync(new PagedResult<Student>
        {
            Items = new List<Student> { StudentWithHierarchy(true, true) },
            Page = 3,
            PageSize = 7,
            TotalCount = 50,
            TotalPages = 8,
        });

        var result = await sut.SearchAsync(new StudentQueryRequest());

        result.Page.Should().Be(3);
        result.PageSize.Should().Be(7);
        result.TotalCount.Should().Be(50);
        result.TotalPages.Should().Be(8);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_CountsActiveAndInactive()
    {
        var (sut, repo, _, _, _) = Build();
        var active1 = StudentWithHierarchy(true, true);
        var active2 = StudentWithHierarchy(true, true);
        var inactive = StudentWithHierarchy(true, true);
        inactive.IsActive = false;
        // Statistics aggregate over the service's own SearchAsync — the
        // repository has no statistics API.
        repo.Setup(r => r.SearchAsync(It.IsAny<StudentQueryRequest>())).ReturnsAsync(new PagedResult<Student>
        {
            Items = new List<Student> { active1, active2, inactive },
            Page = 1,
            PageSize = int.MaxValue,
            TotalCount = 3,
            TotalPages = 1,
        });

        var stats = await sut.GetStatisticsAsync(new UserStatisticsRequest { ScopeNodeId = Guid.NewGuid() });

        stats.TotalStudents.Should().Be(3);
        stats.ActiveStudents.Should().Be(2);
        stats.InactiveStudents.Should().Be(1);
    }
}
