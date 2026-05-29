using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Abstractions.UniversityStructure.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/university-structure")]
public class UniversityStructureController : ControllerBase
{
    private readonly IUniversityStructureService _service;
    private readonly ILocalizationService _localizationService;

    public UniversityStructureController(
        IUniversityStructureService service, ILocalizationService localizationService)
    {
        _service = service;
        _localizationService = localizationService;
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var result = await _service.GetTreeAsync();
        TranslateStructureNodes(result);
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

    [HttpPut("move/{id}")]
    public async Task<IActionResult> Move(
        Guid id,
        [FromBody] MoveStructureNodeRequest request)
    {
        await _service.MoveNodeAsync(id, request);

        return Ok(new
        {
            Message = "Node moved successfully"
        });
    }

    [HttpGet("roots")]
    public async Task<IActionResult> GetRoots()
    {
        var result = await _service.GetRootsAsync();

        return Ok(result);
    }

    [HttpGet("children/{id}")]
    public async Task<IActionResult> GetChildren(Guid id)
    {
        var result = await _service.GetChildrenAsync(id);

        return Ok(result);
    }

    [HttpGet("breadcrumb/{id}")]
    public async Task<IActionResult>
        GetBreadcrumb(Guid id)
    {
        var result = await _service
            .GetBreadcrumbAsync(id);

        return Ok(result);
    }

    [HttpGet("subtree/{id}")]
    public async Task<IActionResult>
        GetSubTree(Guid id)
    {
        var result = await _service
            .GetSubTreeAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("ancestors/{id}")]
    public async Task<IActionResult>
        GetAncestors(Guid id)
    {
        var result = await _service
            .GetAncestorsChainAsync(id);

        return Ok(result);
    }

    [HttpPut("reorder/{id}")]
    public async Task<IActionResult>
        Reorder(
            Guid id,
            [FromBody] ReorderNodeRequest request)
    {
        await _service.ReorderNodeAsync(id, request);

        return Ok(new
        {
            Message = "Node reordered successfully"
        });
    }

    private void TranslateStructureNodes(List<StructureNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            node.TypeNameLocalized = _localizationService.Get(node.Type);
            TranslateStructureNodes(node.Children);
        }
    }
}