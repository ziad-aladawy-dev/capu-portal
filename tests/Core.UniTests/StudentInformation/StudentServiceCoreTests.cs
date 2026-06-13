using CapitalUniversity.Core.Application.Students;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.StudentInformation;

public class StudentServiceCoreTests
{
    private static (StudentService Sut, Mock<IStudentRepository> Repo, Mock<IStructureNodeRepository> Struct, Mock<IPasswordHasher> Pwd, Mock<ILocalizationService> Loc) Build()
    {
        var repo = new Mock<IStudentRepository>();
        var @struct = new Mock<IStructureNodeRepository>();
        var pwd = new Mock<IPasswordHasher>();
        var loc = new Mock<ILocalizationService>();
        var sut = new StudentService(repo.Object, @struct.Object, pwd.Object, loc.Object);
        return (sut, repo, @struct, pwd, loc);
    }

    private static Student StudentWithHierarchy(bool hasProgram, bool hasFaculty)
    {
        var faculty = hasFaculty ? new StructureNode { Name = "FacultyName", Type = StructureNodeType.Faculty } : null;
        var program = hasProgram ? new StructureNode { Name = "ProgramName", Type = StructureNodeType.Program, Parent = faculty } : null;
        var level = new StructureNode { Name = "LevelName", Type = StructureNodeType.Level, Parent = program };
        return new Student { Id = Guid.NewGuid(), Name = "Name", StudentCode = "C1", StructureNode = level, IsActive = true };
    }

    [Fact]
    public async Task GetByIdAsync_MapsHierarchy()
    {
        var (sut, repo, _, _, loc) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(StudentWithHierarchy(true, true));
        loc.Setup(l => l.GetLocalizedString(It.IsAny<string>())).Returns<string>(s => s);

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto!.LevelName.Should().Be("LevelName");
        dto.ProgramName.Should().Be("ProgramName");
        dto.FacultyName.Should().Be("FacultyName");
    }

    [Fact]
    public async Task GetByIdAsync_MissingProgram_LeavesEmpty()
    {
        var (sut, repo, _, _, loc) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(StudentWithHierarchy(false, false));
        loc.Setup(l => l.GetLocalizedString(It.IsAny<string>())).Returns<string>(s => s);

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto!.ProgramName.Should().BeEmpty();
        dto.FacultyName.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_MissingFaculty_LeavesEmpty()
    {
        var (sut, repo, _, _, loc) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(StudentWithHierarchy(true, false));
        loc.Setup(l => l.GetLocalizedString(It.IsAny<string>())).Returns<string>(s => s);

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
        repo.Setup(r => r.SearchAsync(It.IsAny<StudentQueryRequest>())).ReturnsAsync(new PagedResult<StudentDto>
        {
            Items = new List<StudentDto> { new StudentDto { Name = "Name" } },
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
        repo.Setup(r => r.GetStatisticsAsync(It.IsAny<UserStatisticsRequest>())).ReturnsAsync(new UserStatisticsDto
        {
            TotalStudents = 3,
            ActiveStudents = 2,
            InactiveStudents = 1
        });

        var stats = await sut.GetStatisticsAsync(new UserStatisticsRequest { ScopeNodeId = Guid.NewGuid() });

        stats.TotalStudents.Should().Be(3);
        stats.ActiveStudents.Should().Be(2);
        stats.InactiveStudents.Should().Be(1);
    }
}