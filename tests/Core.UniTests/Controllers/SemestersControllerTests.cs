using CapitalUniversity.API.Controllers;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class SemestersControllerTests
{
    private static SemestersController NewController(out Mock<ISemesterService> svc)
    {
        svc = new Mock<ISemesterService>(MockBehavior.Strict);
        return new SemestersController(svc.Object);
    }

    [Fact]
    public async Task GetById_Found_ReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        var response = new SemesterResponse();
        svc.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(response);

        var result = await ctrl.GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((SemesterResponse?)null);

        var result = await ctrl.GetById(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCurrent_Found_ReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var response = new SemesterResponse();
        svc.Setup(s => s.GetCurrentAsync()).ReturnsAsync(response);

        var result = await ctrl.GetCurrent();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task GetCurrent_NotFound_ReturnsNotFoundWithMessage()
    {
        var ctrl = NewController(out var svc);
        svc.Setup(s => s.GetCurrentAsync()).ReturnsAsync((SemesterResponse?)null);

        var result = await ctrl.GetCurrent();

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("No current semester found.", notFound.Value);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var ctrl = NewController(out var svc);
        var req = new CreateSemesterRequest();
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
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        var req = new UpdateSemesterRequest();
        svc.Setup(s => s.UpdateAsync(id, req)).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Update(id, req);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task Delete_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var id = Guid.NewGuid();
        svc.Setup(s => s.DeleteAsync(id)).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.Delete(id);

        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }

    [Fact]
    public async Task RecomputeCurrent_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        svc.Setup(s => s.ResolveCurrentSemesterAsync()).Returns(Task.CompletedTask).Verifiable();

        var result = await ctrl.RecomputeCurrent();

        Assert.IsType<OkObjectResult>(result);
        svc.Verify();
    }
}