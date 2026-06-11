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
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Students;

/// <summary>
/// Core StudentService contract: uniqueness guards (code / email / national-id),
/// password-confirmation, structure-node-must-be-Level enforcement, auto-code
/// generation, bilingual name JSON authoring/merging, and the read-side mapping
/// that walks the Level → Program → Faculty ancestor chain. Every guard message
/// and branch is pinned so string / conditional mutations are killed.
/// </summary>
public class StudentServiceTests
{
    private static (
        StudentService Service,
        Mock<IStudentRepository> Repo,
        Mock<IStructureNodeRepository> Structure,
        Mock<IPasswordHasher> Hasher)
        Build()
    {
        var repo = new Mock<IStudentRepository>();
        var structure = new Mock<IStructureNodeRepository>();
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => $"HASH({p})");

        var sut = new StudentService(repo.Object, structure.Object, hasher.Object, new TestLocalizationService());
        return (sut, repo, structure, hasher);
    }

    private static StructureNode Node(StructureNodeType type, string en, StructureNode? parent = null) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Name = LocalizedJson.Of("ع", en),
        Parent = parent,
    };

    private static CreateStudentRequest ValidCreate(string code = "STU-2000") => new()
    {
        StudentCode = code,
        Password = "p@ss",
        ConfirmPassword = "p@ss",
        NameAr = "طالب",
        NameEn = "Student",
        NationalId = "12345",
        BirthDate = new DateTime(2000, 1, 1),
        PhoneNumber = "0100",
        Email = "s@x.com",
        StructureNodeId = Guid.NewGuid(),
    };

    private static Student ExistingStudent(Guid? id = null, string email = "s@x.com", string nationalId = "111", bool isActive = true)
    {
        var faculty = Node(StructureNodeType.Faculty, "Engineering");
        var program = Node(StructureNodeType.Program, "Software", faculty);
        var level = Node(StructureNodeType.Level, "Level 1", program);
        return new Student
        {
            Id = id ?? Guid.NewGuid(),
            StudentCode = "STU-1",
            Name = JsonSerializer.Serialize(new Dictionary<string, string> { ["ar"] = "اسم", ["en"] = "Name" }),
            NationalId = nationalId,
            Email = email,
            IsActive = isActive,
            StructureNodeId = level.Id,
            StructureNode = level,
        };
    }

    // ---------------- CreateAsync ----------------

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.StudentCodeExistsAsync("STU-2000")).ReturnsAsync(true);

        await sut.Invoking(s => s.CreateAsync(ValidCreate()))
            .Should().ThrowAsync<Exception>().WithMessage("Student code already exists");
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.EmailExistsAsync("s@x.com")).ReturnsAsync(true);

        await sut.Invoking(s => s.CreateAsync(ValidCreate()))
            .Should().ThrowAsync<Exception>().WithMessage("Email already exists");
    }

    [Fact]
    public async Task CreateAsync_DuplicateNationalId_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.NationalIdExistsAsync("12345")).ReturnsAsync(true);

        await sut.Invoking(s => s.CreateAsync(ValidCreate()))
            .Should().ThrowAsync<Exception>().WithMessage("National ID already exists");
    }

    [Fact]
    public async Task CreateAsync_PasswordMismatch_Throws()
    {
        var (sut, _, _, _) = Build();
        var req = ValidCreate();
        req.ConfirmPassword = "different";

        await sut.Invoking(s => s.CreateAsync(req))
            .Should().ThrowAsync<Exception>().WithMessage("Passwords do not match");
    }

    [Fact]
    public async Task CreateAsync_StructureNodeMissing_Throws()
    {
        var (sut, _, structure, _) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);

        await sut.Invoking(s => s.CreateAsync(ValidCreate()))
            .Should().ThrowAsync<Exception>().WithMessage("Structure node not found");
    }

    [Fact]
    public async Task CreateAsync_NodeNotLevel_Throws()
    {
        var (sut, _, structure, _) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Program, "Program"));

        await sut.Invoking(s => s.CreateAsync(ValidCreate()))
            .Should().ThrowAsync<Exception>().WithMessage("Student must be assigned to level node");
    }

    [Fact]
    public async Task CreateAsync_HappyPath_HashesPasswordPersistsAndReturnsId()
    {
        var (sut, repo, structure, hasher) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        Student? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => captured = s);

        var id = await sut.CreateAsync(ValidCreate());

        captured.Should().NotBeNull();
        captured!.PasswordHash.Should().Be("HASH(p@ss)");
        captured.IsActive.Should().BeTrue();
        id.Should().Be(captured.Id);
        hasher.Verify(h => h.HashPassword("p@ss"), Times.Once);
        repo.Verify(r => r.AddAsync(captured), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_BlankNameEn_FallsBackToArabic()
    {
        var (sut, repo, structure, _) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        var req = ValidCreate();
        req.NameEn = "   ";
        Student? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => captured = s);

        await sut.CreateAsync(req);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(captured!.Name)!;
        dict["ar"].Should().Be("طالب");
        dict["en"].Should().Be("طالب"); // fell back to Arabic
    }

    [Fact]
    public async Task CreateAsync_BlankStudentCode_FirstStudent_GeneratesSeedCode()
    {
        var (sut, repo, structure, _) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        repo.Setup(r => r.GetLastStudentCodeAsync()).ReturnsAsync((string?)null);
        var req = ValidCreate(code: "");
        Student? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => captured = s);

        await sut.CreateAsync(req);

        captured!.StudentCode.Should().Be("STU-1001");
    }

    [Fact]
    public async Task CreateAsync_BlankStudentCode_IncrementsLastCode()
    {
        var (sut, repo, structure, _) = Build();
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        repo.Setup(r => r.GetLastStudentCodeAsync()).ReturnsAsync("STU-1005");
        var req = ValidCreate(code: "");
        Student? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Student>())).Callback<Student>(s => captured = s);

        await sut.CreateAsync(req);

        captured!.StudentCode.Should().Be("STU-1006");
    }

    // ---------------- UpdateAsync ----------------

    private static UpdateStudentRequest ValidUpdate(Guid nodeId) => new()
    {
        NameAr = "",
        NameEn = "",
        NationalId = "111",
        BirthDate = new DateTime(1999, 5, 5),
        PhoneNumber = "0111",
        Email = "s@x.com",
        StructureNodeId = nodeId,
        IsActive = true,
    };

    [Fact]
    public async Task UpdateAsync_StudentNotFound_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Student?)null);

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), ValidUpdate(Guid.NewGuid())))
            .Should().ThrowAsync<Exception>().WithMessage("Student not found");
    }

    [Fact]
    public async Task UpdateAsync_StructureNodeMissing_Throws()
    {
        var (sut, repo, structure, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent());
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), ValidUpdate(Guid.NewGuid())))
            .Should().ThrowAsync<Exception>().WithMessage("Structure node not found");
    }

    [Fact]
    public async Task UpdateAsync_NodeNotLevel_Throws()
    {
        var (sut, repo, structure, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent());
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Faculty, "Fac"));

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), ValidUpdate(Guid.NewGuid())))
            .Should().ThrowAsync<Exception>().WithMessage("Student must be assigned to a level");
    }

    [Fact]
    public async Task UpdateAsync_EmailTakenByDifferentStudent_Throws()
    {
        var (sut, repo, structure, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(email: "old@x.com"));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        repo.Setup(r => r.EmailExistsAsync("new@x.com")).ReturnsAsync(true);
        var req = ValidUpdate(Guid.NewGuid());
        req.Email = "new@x.com";

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<Exception>().WithMessage("Email already exists");
    }

    [Fact]
    public async Task UpdateAsync_EmailUnchangedEvenIfExists_DoesNotThrow()
    {
        var (sut, repo, structure, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(email: "same@x.com"));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        repo.Setup(r => r.EmailExistsAsync("same@x.com")).ReturnsAsync(true);
        var req = ValidUpdate(Guid.NewGuid());
        req.Email = "same@x.com";

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), req)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_NationalIdTakenByDifferentStudent_Throws()
    {
        var (sut, repo, structure, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent(nationalId: "111"));
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        repo.Setup(r => r.NationalIdExistsAsync("999")).ReturnsAsync(true);
        var req = ValidUpdate(Guid.NewGuid());
        req.NationalId = "999";

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<Exception>().WithMessage("National ID already exists");
    }

    [Fact]
    public async Task UpdateAsync_PasswordProvidedButMismatch_Throws()
    {
        var (sut, repo, structure, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(ExistingStudent());
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        var req = ValidUpdate(Guid.NewGuid());
        req.Password = "a";
        req.ConfirmPassword = "b";

        await sut.Invoking(s => s.UpdateAsync(Guid.NewGuid(), req))
            .Should().ThrowAsync<Exception>().WithMessage("Passwords do not match");
    }

    [Fact]
    public async Task UpdateAsync_HappyPath_AppliesFieldsAndPersists()
    {
        var (sut, repo, structure, hasher) = Build();
        var student = ExistingStudent();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(student);
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        var req = ValidUpdate(Guid.NewGuid());
        req.Password = "new";
        req.ConfirmPassword = "new";
        req.PhoneNumber = "0999";

        await sut.UpdateAsync(student.Id, req);

        student.PhoneNumber.Should().Be("0999");
        student.PasswordHash.Should().Be("HASH(new)");
        student.UpdatedAt.Should().NotBeNull();
        hasher.Verify(h => h.HashPassword("new"), Times.Once);
        repo.Verify(r => r.UpdateAsync(student), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_MergesNameJson_OnlyOverridesProvidedCulture()
    {
        var (sut, repo, structure, _) = Build();
        var student = ExistingStudent();
        student.Name = JsonSerializer.Serialize(new Dictionary<string, string> { ["ar"] = "قديم", ["en"] = "old" });
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(student);
        structure.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(Node(StructureNodeType.Level, "Level"));
        var req = ValidUpdate(Guid.NewGuid());
        req.NameAr = "جديد"; // only Arabic provided

        await sut.UpdateAsync(student.Id, req);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(student.Name)!;
        dict["ar"].Should().Be("جديد");
        dict["en"].Should().Be("old"); // untouched
    }

    // ---------------- Delete / Toggle ----------------

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        await sut.Invoking(s => s.DeleteAsync(Guid.NewGuid()))
            .Should().ThrowAsync<Exception>().WithMessage("Student not found");
    }

    [Fact]
    public async Task DeleteAsync_Exists_SoftDeletesAndSaves()
    {
        var (sut, repo, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.ExistsAsync(id)).ReturnsAsync(true);

        await sut.DeleteAsync(id);

        repo.Verify(r => r.SoftDeleteAsync(id), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleStatusAsync_NotFound_Throws()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        await sut.Invoking(s => s.ToggleStatusAsync(Guid.NewGuid()))
            .Should().ThrowAsync<Exception>().WithMessage("Student not found");
    }

    [Fact]
    public async Task ToggleStatusAsync_Exists_TogglesAndSaves()
    {
        var (sut, repo, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.ExistsAsync(id)).ReturnsAsync(true);

        await sut.ToggleStatusAsync(id);

        repo.Verify(r => r.ToggleStatusAsync(id), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ---------------- Reads / mapping ----------------

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Student?)null);

        (await sut.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Found_MapsAncestorChainAndLocalizes()
    {
        var (sut, repo, _, _) = Build();
        var student = ExistingStudent();
        repo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);

        var dto = await sut.GetByIdAsync(student.Id);

        dto!.LocalizedName.Should().Be("Name");
        dto.LevelName.Should().Be("Level 1");
        dto.ProgramName.Should().Be("Software");
        dto.FacultyName.Should().Be("Engineering");
        dto.StructureNodeName.Should().Be("Level 1");
    }

    [Fact]
    public async Task GetByIdAsync_LevelWithoutAncestors_LeavesProgramAndFacultyBlank()
    {
        var (sut, repo, _, _) = Build();
        var level = Node(StructureNodeType.Level, "Standalone");
        var student = new Student
        {
            Id = Guid.NewGuid(),
            Name = JsonSerializer.Serialize(new Dictionary<string, string> { ["ar"] = "ا", ["en"] = "Solo" }),
            StructureNodeId = level.Id,
            StructureNode = level,
        };
        repo.Setup(r => r.GetByIdAsync(student.Id)).ReturnsAsync(student);

        var dto = await sut.GetByIdAsync(student.Id);

        dto!.ProgramName.Should().BeEmpty();
        dto.FacultyName.Should().BeEmpty();
        dto.LevelName.Should().Be("Standalone");
    }

    [Fact]
    public async Task GetAllAsync_MapsEveryRow()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student> { ExistingStudent(), ExistingStudent() });

        var all = await sut.GetAllAsync();

        all.Should().HaveCount(2);
        all[0].LocalizedName.Should().Be("Name");
    }

    [Fact]
    public async Task SearchAsync_PassesThroughPagingAndMaps()
    {
        var (sut, repo, _, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<StudentQueryRequest>()))
            .ReturnsAsync(new PagedResult<Student>
            {
                Items = new List<Student> { ExistingStudent() },
                Page = 3,
                PageSize = 10,
                TotalCount = 42,
                TotalPages = 5,
            });

        var result = await sut.SearchAsync(new StudentQueryRequest());

        result.Page.Should().Be(3);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(42);
        result.TotalPages.Should().Be(5);
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetStatisticsAsync_CountsActiveAndInactive()
    {
        // Statistics aggregate over the service's own SearchAsync (full result
        // set, PageSize = int.MaxValue) — the repository has no statistics API.
        var (sut, repo, _, _) = Build();
        // Service computes stats from SearchAsync results, counting IsActive in
        // memory — there is no repository GetStatisticsAsync in the contract.
        repo.Setup(r => r.SearchAsync(It.IsAny<StudentQueryRequest>()))
            .ReturnsAsync(new PagedResult<Student>
            {
                Items = new List<Student>
                {
                    ExistingStudent(isActive: true),
                    ExistingStudent(isActive: true),
                    ExistingStudent(isActive: false),
                },
                Page = 1,
                PageSize = int.MaxValue,
                TotalCount = 3,
                TotalPages = 1,
            });

        var stats = await sut.GetStatisticsAsync(new UserStatisticsRequest());

        stats.TotalStudents.Should().Be(3);
        stats.ActiveStudents.Should().Be(2);
        stats.InactiveStudents.Should().Be(1);
    }
}
