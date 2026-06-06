using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class CoursesControllerTests
{
    private static CoursesController NewController(out Mock<ICourseService> svc)
    {
        svc = new Mock<ICourseService>(MockBehavior.Strict);
        return new CoursesController(svc.Object);
    }

    [Fact]
    public async Task GetActive_ReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var list = (IReadOnlyList<CourseResponse>)new List<CourseResponse> { new() };
        svc.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var result = await ctrl.GetActive(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        var response = new CourseResponse();
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
        svc.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((CourseResponse?)null);

        var result = await ctrl.GetById(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var ctrl = NewController(out var svc);
        var req = new CreateCourseRequest();
        var id = Guid.NewGuid();
        svc.Setup(s => s.CreateAsync(req, It.IsAny<CancellationToken>())).ReturnsAsync(id);

        var result = await ctrl.Create(req, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ctrl.GetById), created.ActionName);
        Assert.Equal(id, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Update_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        var req = new UpdateCourseRequest();
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
}