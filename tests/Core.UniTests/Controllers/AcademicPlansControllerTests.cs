using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class AcademicPlansControllerTests
{
    private static AcademicPlansController NewController(out Mock<IAcademicPlanService> svc)
    {
        svc = new Mock<IAcademicPlanService>(MockBehavior.Strict);
        return new AcademicPlansController(svc.Object);
    }

    [Fact]
    public async Task GetById_Found_ReturnsOkWithBody()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        var response = new AcademicPlanResponse();
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await ctrl.GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AcademicPlanResponse?)null);

        var result = await ctrl.GetById(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetForStructureNode_ReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var structureNodeId = Guid.NewGuid();
        var list = (IReadOnlyList<AcademicPlanResponse>)new List<AcademicPlanResponse> { new() };
        svc.Setup(s => s.GetForStructureNodeAsync(structureNodeId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var result = await ctrl.GetForStructureNode(structureNodeId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var ctrl = NewController(out var svc);
        var req = new CreateAcademicPlanRequest();
        var newId = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(req, It.IsAny<CancellationToken>())).ReturnsAsync(newId);

        var result = await ctrl.Create(req, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ctrl.GetById), created.ActionName);
        Assert.Equal(newId, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Update_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        var req = new UpdateAcademicPlanRequest();
        svc.Setup(s => s.UpdateAsync(id, req, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Update(id, req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task Delete_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Delete(id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task AddCourse_ReturnsOkWithGeneratedId()
    {
        var ctrl = NewController(out var svc);
        var planId = Guid.NewGuid();
        var planCourseId = Guid.NewGuid();
        var req = new AddPlanCourseRequest();
        svc.Setup(s => s.AddCourseAsync(planId, req, It.IsAny<CancellationToken>())).ReturnsAsync(planCourseId);

        var result = await ctrl.AddCourse(planId, req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RemoveCourse_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var planId = Guid.NewGuid();
        var planCourseId = Guid.NewGuid();
        svc.Setup(s => s.RemoveCourseAsync(planId, planCourseId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.RemoveCourse(planId, planCourseId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }
}