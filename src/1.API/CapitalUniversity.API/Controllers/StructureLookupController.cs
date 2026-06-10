using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.Api.Controllers;

[ApiController]
[Route("api/structure/lookups")]
public class StructureLookupController : ControllerBase
{
    private readonly IStructureLookupService _service;

    public StructureLookupController(
        IStructureLookupService service)
    {
        _service = service;
    }

    [HttpGet("faculties")]
    public async Task<IActionResult> GetFaculties()
    {
        var result = await _service.GetByTypeAsync(
            StructureNodeType.Faculty);

        return Ok(result);
    }

    [HttpGet("systems")]
    public async Task<IActionResult> GetSystems()
    {
        var result = await _service.GetByTypeAsync(
            StructureNodeType.System);

        return Ok(result);
    }

    [HttpGet("programs")]
    public async Task<IActionResult> GetPrograms()
    {
        var result = await _service.GetByTypeAsync(
            StructureNodeType.Program);

        return Ok(result);
    }

    [HttpGet("levels")]
    public async Task<IActionResult> GetLevels()
    {
        var result = await _service.GetByTypeAsync(
            StructureNodeType.Level);

        return Ok(result);
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var result = await _service.GetByTypeAsync(
            StructureNodeType.Department);

        return Ok(result);
    }

    [HttpGet("specializations")]
    public async Task<IActionResult> GetSpecializations()
    {
        var result = await _service.GetByTypeAsync(
            StructureNodeType.Specialization);

        return Ok(result);
    }

    [HttpGet("{parentId}/children")]
    public async Task<IActionResult> GetChildren(
        Guid parentId)
    {
        var result = await _service
            .GetChildrenAsync(parentId);

        return Ok(result);
    }

    [HttpGet("{parentId}/children/{type}")]
    public async Task<IActionResult> GetChildrenByType(
        Guid parentId,
        StructureNodeType type)
    {
        var result = await _service
            .GetChildrenByTypeAsync(
                parentId,
                type);

        return Ok(result);
    }

    [HttpGet("faculties/{facultyId}/programs")]
    public async Task<IActionResult> GetProgramsByFaculty(Guid facultyId)
    {
        var result = await _service.GetProgramsByFacultyAsync(facultyId);
        return Ok(result);
    }
}
