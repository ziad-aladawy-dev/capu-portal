using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Application.StaffManagement;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.StaffManagement;

public class StaffServiceTests
{
    private static (StaffService Sut, Mock<IStaffRepository> Staff, Mock<IStructureNodeRepository> Struct, Mock<IPasswordHasher> Pwd, Mock<ILocalizationService> Loc, Mock<ISessionVersionService> Svc, Mock<IUnitOfWork> Uow) Build()
    {
        var staff = new Mock<IStaffRepository>();
        var @struct = new Mock<IStructureNodeRepository>();
        var pwd = new Mock<IPasswordHasher>();
        var loc = new Mock<ILocalizationService>();
        var svc = new Mock<ISessionVersionService>();
        var uow = new Mock<IUnitOfWork>();
        var sut = new StaffService(staff.Object, @struct.Object, pwd.Object, loc.Object, svc.Object, uow.Object);
        return (sut, staff, @struct, pwd, loc, svc, uow);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNull()
    {
        var (sut, staff, _, _, _, _, _) = Build();
        staff.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Staff?)null);

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetStatistics_CountsActiveAndInactiveSeparately()
    {
        var (sut, staff, _, _, _, _, _) = Build();
        staff.Setup(r => r.GetStatisticsAsync(It.IsAny<UserStatisticsRequest>())).ReturnsAsync(new UserStatisticsDto
        {
            TotalStaff = 3,
            ActiveStaff = 2,
            InactiveStaff = 1
        });

        var stats = await sut.GetStatisticsAsync(new UserStatisticsRequest());

        stats.TotalStaff.Should().Be(3);
        stats.ActiveStaff.Should().Be(2);
        stats.InactiveStaff.Should().Be(1);
    }
}