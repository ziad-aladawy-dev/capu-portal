using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Application.StaffManagement;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.UniTests.StaffManagement;

/// <summary>
/// StaffService contract: enforces (email, national-id, employee-code,
/// password-match, structure-node) uniqueness/validity invariants on create,
/// auto-generates monotonically-incremented employee codes from the prior
/// "EMP-N" max, invalidates the SessionVersion cache on create so the new
/// staff's first authenticated request resolves cleanly, persists on every
/// mutation, and on update preserves identity fields unless the corresponding
/// value passed validation.
/// </summary>
public class StaffServiceTests
{
    private static (StaffService Sut, Mock<IStaffRepository> Staff, Mock<IStructureNodeRepository> Structure, Mock<ISessionVersionService> Sessions, Mock<IUnitOfWork> Uow) Build()
    {
        var staff = new Mock<IStaffRepository>();
        var structure = new Mock<IStructureNodeRepository>();
        var sessions = new Mock<ISessionVersionService>();
        var uow = new Mock<IUnitOfWork>();
        staff.Setup(r => r.EmailExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        staff.Setup(r => r.NationalIdExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        staff.Setup(r => r.EmployeeCodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        staff.Setup(r => r.GetLastEmployeeCodeAsync()).ReturnsAsync((string?)null);
        structure.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new StructureNode { Id = Guid.NewGuid(), Name = "Faculty of CS" });
        
        var sut = new StaffService(
            staff.Object,
            structure.Object,
            new Mock<IPasswordHasher>().Object,
            new TestLocalizationService(),
            sessions.Object,
            uow.Object);
            
        return (sut, staff, structure, sessions, uow);
    }

    private static CreateStaffRequest ValidCreateRequest(Guid? structureNodeId = null) => new()
    {
        EmployeeCode = "EMP-2001",
        Password = "P@ss",
        ConfirmPassword = "P@ss",
        NameAr = "Aya",
        NameEn = "Aya",
        NationalId = "29901011234567",
        BirthDate = new DateTime(1999, 1, 1),
        PhoneNumber = "+201111111111",
        Email = "aya@example.com",
        Role = "Lecturer",
        JobTitle = "Assistant Lecturer",
        StructureNodeId = structureNodeId ?? Guid.NewGuid(),
    };

    [Fact]
    public async Task Create_HappyPath_PersistsAndInvalidatesSessionCache()
    {
        var (sut, staff, _, sessions, uow) = Build();
        Staff? captured = null;
        staff.Setup(r => r.AddAsync(It.IsAny<Staff>())).Callback<Staff>(s => captured = s).Returns(Task.CompletedTask);

        var id = await sut.CreateAsync(ValidCreateRequest());

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.Id.Should().Be(id);
        captured.EmployeeCode.Should().Be("EMP-2001");
        captured.Name.Should().Be("{\"ar\":\"Aya\",\"en\":\"Aya\"}");
        captured.IsActive.Should().BeTrue("newly-created staff are active by default");
        staff.Verify(r => r.SaveChangesAsync(), Times.Once);
        sessions.Verify(s => s.InvalidateCacheAsync(id, default), Times.Once,
            "P2.6 — negative-cached 'not found' must be evicted so first auth call resolves");
    }

    [Fact]
    public async Task Create_DuplicateEmail_Throws()
    {
        var (sut, staff, _, _, uow) = Build();
        staff.Setup(r => r.EmailExistsAsync("aya@example.com")).ReturnsAsync(true);

        var act = async () => await sut.CreateAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<Exception>().WithMessage("*Email*exists*");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_DuplicateNationalId_Throws()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.NationalIdExistsAsync("29901011234567")).ReturnsAsync(true);

        var act = async () => await sut.CreateAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<Exception>().WithMessage("*National ID*exists*");
    }

    [Fact]
    public async Task Create_PasswordMismatch_Throws()
    {
        var (sut, _, _, _, _) = Build();
        var req = ValidCreateRequest();
        req.ConfirmPassword = "different";

        var act = async () => await sut.CreateAsync(req);

        await act.Should().ThrowAsync<Exception>().WithMessage("*Passwords*do not match*");
    }

    [Fact]
    public async Task Create_UnknownStructureNode_Throws()
    {
        var (sut, _, structure, _, _) = Build();
        structure.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((StructureNode?)null);

        var act = async () => await sut.CreateAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<Exception>().WithMessage("*Structure node not found*");
    }

    [Fact]
    public async Task Create_DuplicateEmployeeCode_Throws()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.EmployeeCodeExistsAsync("EMP-2001")).ReturnsAsync(true);

        var act = async () => await sut.CreateAsync(ValidCreateRequest());

        await act.Should().ThrowAsync<Exception>().WithMessage("*Employee code*exists*");
    }

    [Fact]
    public async Task Create_BlankEmployeeCode_FirstStaff_Generates_EMP_1001()
    {
        var (sut, staff, _, _, _) = Build();
        Staff? captured = null;
        staff.Setup(r => r.AddAsync(It.IsAny<Staff>())).Callback<Staff>(s => captured = s).Returns(Task.CompletedTask);
        var req = ValidCreateRequest();
        req.EmployeeCode = "";

        await sut.CreateAsync(req);

        captured!.EmployeeCode.Should().Be("EMP-1001",
            "the seed code when no prior staff exists must be deterministic and human-recognizable");
    }

    [Fact]
    public async Task Create_BlankEmployeeCode_PriorExists_GeneratesIncrementedCode()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.GetLastEmployeeCodeAsync()).ReturnsAsync("EMP-4047");
        Staff? captured = null;
        staff.Setup(r => r.AddAsync(It.IsAny<Staff>())).Callback<Staff>(s => captured = s).Returns(Task.CompletedTask);
        var req = ValidCreateRequest();
        req.EmployeeCode = "   ";

        await sut.CreateAsync(req);

        captured!.EmployeeCode.Should().Be("EMP-4048",
            "the next code must be the previous maximum + 1 to preserve monotonicity");
    }

    [Fact]
    public async Task Update_NonexistentStaff_Throws()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Staff?)null);

        var act = async () => await sut.UpdateAsync(Guid.NewGuid(), new UpdateStaffRequest
        {
            Email = "x@y.z", NationalId = "1", NameAr = "X", NameEn = "X", PhoneNumber = "1",
            Role = "r", JobTitle = "j", StructureNodeId = Guid.NewGuid(),
            BirthDate = DateTime.UtcNow, IsActive = true,
        });

        await act.Should().ThrowAsync<Exception>().WithMessage("*Staff not found*");
    }

    [Fact]
    public async Task Update_EmailUnchanged_DoesNotThrow_EvenIfEmailExistsForSelf()
    {
        var (sut, staff, _, _, uow) = Build();
        var id = Guid.NewGuid();
        var existing = new Staff { Id = id, Email = "self@x", NationalId = "1", Name = "old", PhoneNumber = "1" };
        staff.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existing);
        staff.Setup(r => r.EmailExistsAsync("self@x")).ReturnsAsync(true);

        await sut.UpdateAsync(id, new UpdateStaffRequest
        {
            Email = "self@x", NationalId = "1", NameAr = "new", NameEn = "new", PhoneNumber = "1",
            Role = "r", JobTitle = "j", StructureNodeId = Guid.NewGuid(),
            BirthDate = new DateTime(2000, 1, 1), IsActive = true,
        });

        existing.Name.Should().Contain("new");
        staff.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_EmailTakenByOtherStaff_Throws()
    {
        var (sut, staff, _, _, _) = Build();
        var id = Guid.NewGuid();
        staff.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(new Staff { Id = id, Email = "self@x", NationalId = "1" });
        staff.Setup(r => r.EmailExistsAsync("other@x")).ReturnsAsync(true);

        var act = async () => await sut.UpdateAsync(id, new UpdateStaffRequest
        {
            Email = "other@x", NationalId = "1", NameAr = "n", NameEn = "n", PhoneNumber = "1",
            Role = "r", JobTitle = "j", StructureNodeId = Guid.NewGuid(),
            BirthDate = new DateTime(2000, 1, 1), IsActive = true,
        });

        await act.Should().ThrowAsync<Exception>().WithMessage("*Email already exists*");
    }

    [Fact]
    public async Task Update_PasswordProvided_MismatchThrows()
    {
        var (sut, staff, _, _, _) = Build();
        var id = Guid.NewGuid();
        staff.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(new Staff { Id = id, Email = "x", NationalId = "1" });

        var act = async () => await sut.UpdateAsync(id, new UpdateStaffRequest
        {
            Email = "x", NationalId = "1", NameAr = "n", NameEn = "n", PhoneNumber = "1",
            Role = "r", JobTitle = "j", StructureNodeId = Guid.NewGuid(),
            BirthDate = new DateTime(2000, 1, 1), IsActive = true,
            Password = "abc", ConfirmPassword = "xyz",
        });

        await act.Should().ThrowAsync<Exception>().WithMessage("*Passwords do not match*");
    }

    [Fact]
    public async Task Update_NoPassword_LeavesPasswordHashUnchanged()
    {
        var (sut, staff, _, _, _) = Build();
        var id = Guid.NewGuid();
        var existing = new Staff { Id = id, Email = "x", NationalId = "1", PasswordHash = "preserved" };
        staff.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existing);

        await sut.UpdateAsync(id, new UpdateStaffRequest
        {
            Email = "x", NationalId = "1", NameAr = "n", NameEn = "n", PhoneNumber = "1",
            Role = "r", JobTitle = "j", StructureNodeId = Guid.NewGuid(),
            BirthDate = new DateTime(2000, 1, 1), IsActive = true,
        });

        existing.PasswordHash.Should().Be("preserved",
            "a blank password in an update must NOT clear the existing hash — that would lock out the user");
    }

    [Fact]
    public async Task Delete_Existing_CallsSoftDeleteAndPersists()
    {
        var (sut, staff, _, _, uow) = Build();
        var id = Guid.NewGuid();
        staff.Setup(r => r.ExistsAsync(id)).ReturnsAsync(true);

        await sut.DeleteAsync(id);

        staff.Verify(r => r.SoftDeleteAsync(id), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Delete_Nonexistent_Throws()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var act = async () => await sut.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>().WithMessage("*Staff not found*");
    }

    [Fact]
    public async Task ToggleStatus_Nonexistent_Throws()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        var act = async () => await sut.ToggleStatusAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>().WithMessage("*Staff not found*");
    }

    [Fact]
    public async Task GetById_Existing_ProjectsAllFields()
    {
        var (sut, staff, _, _, _) = Build();
        var faculty = new StructureNode { Id = Guid.NewGuid(), Type = StructureNodeType.Faculty, Name = "{\"ar\":\"Faculty of Engineering\",\"en\":\"Faculty of Engineering\"}" };
        var node = new StructureNode { Id = Guid.NewGuid(), Name = "{\"ar\":\"Computer Science Dept\",\"en\":\"Computer Science Dept\"}", Parent = faculty };
        var entity = new Staff
        {
            Id = Guid.NewGuid(),
            EmployeeCode = "EMP-9000",
            Name = "{\"ar\":\"Ali\",\"en\":\"Ali\"}",
            NationalId = "1",
            BirthDate = new DateTime(1990, 6, 1),
            PhoneNumber = "+20",
            Email = "ali@x",
            Role = "Lecturer",
            JobTitle = "{\"ar\":\"Senior\",\"en\":\"Senior\"}",
            StructureNodeId = node.Id,
            StructureNode = node,
            PasswordExpiry = new DateTime(2030, 1, 1),
            IsActive = true,
        };
        staff.Setup(r => r.GetByIdAsync(entity.Id)).ReturnsAsync(entity);

        var dto = await sut.GetByIdAsync(entity.Id);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(entity.Id);
        dto.EmployeeCode.Should().Be("EMP-9000");
        dto.StructureNodeName.Should().Be("Computer Science Dept");
        dto.FacultyName.Should().Be("Faculty of Engineering",
            "Faculty name is the parent of the staff's structure node and must come through the projection");
    }

    [Fact]
    public async Task GetById_OrphanNode_FacultyNameEmpty()
    {
        var (sut, staff, _, _, _) = Build();
        var rootNode = new StructureNode { Id = Guid.NewGuid(), Name = "{\"ar\":\"Faculty\",\"en\":\"Faculty\"}", Parent = null };
        var entity = new Staff
        {
            Id = Guid.NewGuid(), Email = "x", NationalId = "1",
            StructureNode = rootNode, StructureNodeId = rootNode.Id,
        };
        staff.Setup(r => r.GetByIdAsync(entity.Id)).ReturnsAsync(entity);

        var dto = await sut.GetByIdAsync(entity.Id);

        dto!.FacultyName.Should().BeEmpty();
        dto.StructureNodeName.Should().Be("Faculty");
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNull()
    {
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Staff?)null);

        var dto = await sut.GetByIdAsync(Guid.NewGuid());

        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetStatistics_CountsActiveAndInactiveSeparately()
    {
        // Statistics aggregate over the service's own SearchAsync (full result
        // set) — the repository has no statistics API.
        var (sut, staff, _, _, _) = Build();
        staff.Setup(r => r.SearchAsync(It.IsAny<StaffQueryRequest>())).ReturnsAsync(new PagedResult<Staff>
        {
            Items = new List<Staff> { MakeStaff(true), MakeStaff(true), MakeStaff(false) },
            Page = 1,
            PageSize = int.MaxValue,
            TotalCount = 3,
            TotalPages = 1,
        });

        var stats = await sut.GetStatisticsAsync(new UserStatisticsRequest());

        stats.TotalStaff.Should().Be(3);
        stats.ActiveStaff.Should().Be(2);
        stats.InactiveStaff.Should().Be(1);
    }

    private static Staff MakeStaff(bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeCode = "E1",
        Name = "{\"ar\":\"ن\",\"en\":\"N\"}",
        NationalId = "1",
        Email = "e@x",
        PhoneNumber = "1",
        Role = "r",
        JobTitle = "{\"ar\":\"و\",\"en\":\"J\"}",
        IsActive = isActive,
    };

    [Fact]
    public async Task GetStatistics_RequestsAllPages_NotJustFirstPage()
    {
        var (sut, staff, _, _, _) = Build();
        // Stats must count across the whole tenant, so the service requests an
        // unbounded page from SearchAsync (PageSize == int.MaxValue), not page 1.
        StaffQueryRequest? observed = null;
        staff.Setup(r => r.SearchAsync(It.IsAny<StaffQueryRequest>()))
            .Callback<StaffQueryRequest>(q => observed = q)
            .ReturnsAsync(new PagedResult<Staff> { Items = new List<Staff>() });

        await sut.GetStatisticsAsync(new UserStatisticsRequest());

        observed.Should().NotBeNull();
        observed!.Page.Should().Be(1);
        observed.PageSize.Should().Be(int.MaxValue,
            "statistics must aggregate the full result set, not the first page");
    }
}