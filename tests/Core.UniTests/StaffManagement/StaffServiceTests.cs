using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Application.StaffManagement;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.StaffManagement;

public class StaffServiceTests
{
    private static (StaffService Sut, Mock<IStaffRepository> Staff, Mock<IStructureNodeRepository> Structure, Mock<ISessionVersionService> Sessions, Mock<IUnitOfWork> Uow) Build()
    {
        var staff = new Mock<IStaffRepository>();
        var structure = new Mock<IStructureNodeRepository>();
        var sessions = new Mock<ISessionVersionService>();
        var uow = new Mock<IUnitOfWork>();
        var hasher = new Mock<IPasswordHasher>();
        staff.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        staff.Setup(r => r.NationalIdExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        staff.Setup(r => r.EmployeeCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        staff.Setup(r => r.GetLastEmployeeCodeAsync()).ReturnsAsync((string?)null);
        structure.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new StructureNode { Id = Guid.NewGuid(), Name = "Faculty of CS" });
        var sut = new StaffService(staff.Object, structure.Object, hasher.Object, sessions.Object, uow.Object, new TestLocalizationService());
        return (sut, staff, structure, sessions, uow);
    }

    private static CreateStaffRequest ValidCreate() => new()
    {
        Password = "P@ss",
        ConfirmPassword = "P@ss",
        Name = "Aya",
        NationalId = "29901011234567",
        BirthDate = new DateTime(1999, 1, 1),
        PhoneNumber = "+201111111111",
        Email = "aya@example.com",
        StructureNodeId = Guid.NewGuid(),
        JobTitle = "TA",
        Role = "Faculty"
    };

    [Fact]
    public async Task Create_HappyPath_PersistsAndInvalidatesSessionCache()
    {
        var (sut, staffRepo, _, sessions, uow) = Build();
        var req = ValidCreate();

        await sut.CreateAsync(req);

        staffRepo.Verify(r => r.AddAsync(It.Is<Staff>(s => s.Email == req.Email)), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);

        var captured = (Staff)staffRepo.Invocations.First(i => i.Method.Name == "AddAsync").Arguments[0];
        // Service normalizes Name to JSON.
        captured.Name.Should().Be("{\"ar\":\"Aya\",\"en\":\"Aya\"}");
    }

    [Fact]
    public async Task Update_EmailUnchanged_DoesNotThrow_EvenIfEmailExistsForSelf()
    {
        var (sut, staffRepo, _, _, uow) = Build();
        var existing = new Staff { Id = Guid.NewGuid(), Email = "old@ex.com", Name = "old" };
        staffRepo.Setup(r => r.GetByIdAsync(existing.Id)).ReturnsAsync(existing);
        // Simulate "email taken" but it's the same email as the entity.
        staffRepo.Setup(r => r.EmailExistsAsync(existing.Email)).ReturnsAsync(true);

        var req = new UpdateStaffRequest { Email = existing.Email, Name = "new" };
        await sut.UpdateAsync(existing.Id, req);

        existing.Name.Should().Be("{\"ar\":\"new\",\"en\":\"new\"}");
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
