using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.Api.Controllers;

[ApiController]
[Route("api/students")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _service;

    public StudentsController(
        IStudentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service
            .GetByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentRequest request)
    {
        var id = await _service
            .CreateAsync(request);

        return Ok(new
        {
            Id = id,
            Message = "Student created successfully"
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStudentRequest request)
    {
        await _service.UpdateAsync(id, request);

        return Ok(new
        {
            Message = "Student updated successfully"
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);

        return Ok(new
        {
            Message = "Student deleted successfully"
        });
    }
}