using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Student-facing catalog endpoints. Only active services are surfaced. The
/// admin variants (<see cref="StudentServicesAdminController"/>) handle the
/// full catalog with insert/update/delete.
/// </summary>
[ApiController]
[Route("api/student-services")]
public class StudentServicesController : ControllerBase
{
    private readonly IStudentServiceService _service;

    public StudentServicesController(IStudentServiceService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(PermissionNames.StudentServicesCatalog.View)]
    public async Task<IActionResult> GetAvailable(CancellationToken cancellationToken)
    {
        var result = await _service.GetAvailableAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.StudentServicesCatalog.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null || !result.IsActive) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Paged catalog search. Filters: <c>search</c> (free-text name/code),
    /// <c>isActive</c>. Surfaces the existing service-layer pagination that
    /// previously had no controller endpoint.
    /// </summary>
    [HttpGet("search")]
    [HasPermission(PermissionNames.StudentServicesCatalog.View)]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;
        var result = await _service.GetAllAsync(page, pageSize, search, isActive, cancellationToken);
        return Ok(result);
    }
}
