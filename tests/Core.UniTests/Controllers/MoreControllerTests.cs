using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class AcademicYearsControllerTests
{
    private static AcademicYearsController Build(out Mock<IAcademicYearService> svc)
    {
        svc = new Mock<IAcademicYearService>(MockBehavior.Strict);
        return new AcademicYearsController(svc.Object);
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var ctrl = Build(out var svc);
        var list = (IEnumerable<AcademicYearResponse>)new List<AcademicYearResponse> { new() };
        svc.Setup(s => s.GetAllAsync()).ReturnsAsync(list);

        var result = await ctrl.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        var resp = new AcademicYearResponse();
        svc.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(resp);

        var result = await ctrl.GetById(id);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(resp, ok.Value);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((AcademicYearResponse?)null);

        var result = await ctrl.GetById(id);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var ctrl = Build(out var svc);
        var req = new CreateAcademicYearRequest();
        var newId = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(req)).ReturnsAsync(newId);

        var result = await ctrl.Create(req);
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ctrl.GetById), created.ActionName);
        Assert.Equal(newId, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Update_DelegatesAndReturnsOk()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        var req = new UpdateAcademicYearRequest();
        svc.Setup(s => s.UpdateAsync(id, req)).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Update(id, req);
        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task Delete_DelegatesAndReturnsOk()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id)).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Delete(id);
        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task Resolve_DelegatesAndReturnsOk()
    {
        var ctrl = Build(out var svc);
        svc.Setup(s => s.ResolveCurrentYearAsync()).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Resolve();
        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task GetSemesters_DelegatesToSemesterService()
    {
        var ctrl = Build(out _);
        var id = Guid.NewGuid();
        var sem = new Mock<ISemesterService>(MockBehavior.Strict);
        var list = (IEnumerable<SemesterResponse>)new List<SemesterResponse> { new() };
        sem.Setup(s => s.GetByAcademicYearIdAsync(id)).ReturnsAsync(list);

        var result = await ctrl.GetSemesters(id, sem.Object);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }
}

public class InvoicesControllerTests
{
    private static InvoicesController Build(out Mock<IInvoiceService> svc)
    {
        svc = new Mock<IInvoiceService>(MockBehavior.Strict);
        return new InvoicesController(svc.Object);
    }

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        var resp = new InvoiceResponse();
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(resp);

        var result = await ctrl.GetById(id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(resp, ok.Value);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((InvoiceResponse?)null);

        var result = await ctrl.GetById(id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetForStudent_ReturnsOk()
    {
        var ctrl = Build(out var svc);
        var studentId = Guid.NewGuid();
        var list = (IReadOnlyList<InvoiceResponse>)new List<InvoiceResponse> { new() };
        svc.Setup(s => s.GetForStudentAsync(studentId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var result = await ctrl.GetForStudent(studentId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var ctrl = Build(out var svc);
        var req = new CreateInvoiceRequest();
        var newId = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(req, It.IsAny<CancellationToken>())).ReturnsAsync(newId);

        var result = await ctrl.Create(req, CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ctrl.GetById), created.ActionName);
        Assert.Equal(newId, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Cancel_DelegatesAndReturnsOk()
    {
        var ctrl = Build(out var svc);
        var id = Guid.NewGuid();
        var req = new CancelInvoiceRequest();
        svc.Setup(s => s.CancelAsync(id, req, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Cancel(id, req, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }
}

public class PermissionsControllerEndpointTests
{
    private static PermissionsController Build(out Mock<IPermissionManagementService> svc, out Mock<ICurrentUser> user)
    {
        svc = new Mock<IPermissionManagementService>(MockBehavior.Strict);
        user = new Mock<ICurrentUser>();
        return new PermissionsController(svc.Object, user.Object);
    }

    [Fact]
    public async Task GetEffectivePermissions_DelegatesToCurrentUser()
    {
        var ctrl = Build(out var svc, out var user);
        var uid = Guid.NewGuid();
        user.Setup(u => u.Id).Returns(uid);
        var perms = new List<PermissionDto> { new() };
        svc.Setup(s => s.GetEffectivePermissionsAsync(uid, It.IsAny<CancellationToken>())).ReturnsAsync(perms);

        var result = await ctrl.GetEffectivePermissions(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(perms, ok.Value);
    }

    [Fact]
    public async Task GetAssignment_Found_ReturnsOk()
    {
        var ctrl = Build(out var svc, out _);
        var query = new GetPermissionAssignmentQueryDto();
        var assignment = new PermissionAssignmentResponse();
        svc.Setup(s => s.GetAssignmentAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(assignment);

        var result = await ctrl.GetAssignment(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(assignment, ok.Value);
    }

    [Fact]
    public async Task GetAssignment_NotFound_ReturnsNotFound()
    {
        var ctrl = Build(out var svc, out _);
        var query = new GetPermissionAssignmentQueryDto();
        svc.Setup(s => s.GetAssignmentAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync((PermissionAssignmentResponse?)null);

        var result = await ctrl.GetAssignment(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(okResult.Value);
    }

    [Fact]
    public async Task CreateAssignment_DelegatesAndReturnsOk()
    {
        var ctrl = Build(out var svc, out _);
        var req = new CreatePermissionAssignmentRequest();
        var resp = new PermissionAssignmentResponse();
        svc.Setup(s => s.CreateAssignmentAsync(req, It.IsAny<CancellationToken>())).ReturnsAsync(resp);

        var result = await ctrl.CreateAssignment(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(resp, ok.Value);
    }

    [Fact]
    public async Task UpdateAssignment_DelegatesAndReturnsOk()
    {
        var ctrl = Build(out var svc, out _);
        var req = new UpdatePermissionAssignmentRequest();
        var resp = new PermissionAssignmentResponse();
        svc.Setup(s => s.UpdateAssignmentAsync(req, It.IsAny<CancellationToken>())).ReturnsAsync(resp);

        var result = await ctrl.UpdateAssignment(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(resp, ok.Value);
    }
}
