using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/university-structure")]
public class UniversityStructureController : ControllerBase
{
    private readonly IUniversityStructureService _service;

    public UniversityStructureController(
        IUniversityStructureService service)
    {
        _service = service;
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var result = await _service.GetTreeAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStructureNodeRequest request)
    {
        var id = await _service.CreateNodeAsync(request);

        return Ok(new
        {
            Message = "Node created successfully",
            Id = id
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStructureNodeRequest request)
    {
        await _service.UpdateNodeAsync(id, request);

        return Ok(new
        {
            Message = "Node updated successfully"
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteNodeAsync(id);

        return Ok(new
        {
            Message = "Node deleted successfully"
        });
    }
}