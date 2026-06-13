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

namespace CapitalUniversity.Core.UniTests.Students;

public class StudentServiceTests
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

    private static Student ExistingStudent(Guid? id = null, string code = "STU101", string name = "Name", bool isActive = true)
    {
        return new Student
        {
            Id = id ?? Guid.NewGuid(),
            StudentCode = code,
            Name = name,
            IsActive = isActive,
            StructureNode = new StructureNode { Name = "Level 1" }
        };
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMappedDto()
    {
        var (sut, repo, _, _, loc) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(ExistingStudent(id));
        loc.Setup(l => l.GetLocalizedString(It.IsAny<string>())).Returns("Name");

        var result = await sut.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.LocalizedName.Should().Be("Name");
    }

    [Fact]
    public async Task GetAllAsync_MapsEveryRow()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Student> { ExistingStudent(), ExistingStudent() });

        var all = await sut.GetAllAsync();

        all.Should().HaveCount(2);
        all[0].LocalizedName.Should().Be("Name");
    }

    [Fact]
    public async Task SearchAsync_PassesThroughPagingAndMaps()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.SearchAsync(It.IsAny<StudentQueryRequest>()))
            .ReturnsAsync(new PagedResult<StudentDto>
            {
                Items = new List<StudentDto> { new StudentDto { Id = Guid.NewGuid(), Name = "Test" } },
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
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetStatisticsAsync(It.IsAny<UserStatisticsRequest>()))
            .ReturnsAsync(new UserStatisticsDto
            {
                TotalStudents = 3,
                ActiveStudents = 2,
                InactiveStudents = 1
            });

        var stats = await sut.GetStatisticsAsync(new UserStatisticsRequest());

        stats.TotalStudents.Should().Be(3);
        stats.ActiveStudents.Should().Be(2);
        stats.InactiveStudents.Should().Be(1);
    }
}