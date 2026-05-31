//using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace CapitalUniversity.Module.StudentServices.Host.Controllers;

[ApiController]
[Route("api/student-services/services")]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly IServiceManagementService _service;

    public ServicesController(IServiceManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    //[HasPermission("student-services.services.View")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllActiveServicesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    //[HasPermission("student-services.services.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetServiceAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    //[HasPermission("student-services.services.Insert")]
    public async Task<IActionResult> Create([FromBody] CreateServiceDto dto, CancellationToken cancellationToken)
    {
        var id = await _service.CreateServiceAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    //[HasPermission("student-services.services.EditClose")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateServiceAsync(id, dto, cancellationToken);
        return Ok(new { message = "Service updated" });
    }

    [HttpDelete("{id:guid}")]
    //[HasPermission("student-services.services.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteServiceAsync(id, cancellationToken);
        return Ok(new { message = "Service deleted" });
    }

    [HttpPatch("{id:guid}/toggle")]
    //[HasPermission("student-services.services.EditClose")]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        await _service.ToggleServiceStatusAsync(id, cancellationToken);
        return Ok(new { message = "Status toggled" });
    }

    [HttpGet("available")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableForStudent([FromQuery] Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAvailableServicesForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }
}