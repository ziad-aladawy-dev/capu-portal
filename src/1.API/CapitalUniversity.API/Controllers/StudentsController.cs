using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using ClosedXML.Excel;
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

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] StudentQueryRequest request)
    {
        var result = await _service.SearchAsync(request);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
            return NotFound(new
            {
                Message = "Student not found"
            });

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

    [HttpPatch("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        await _service.ToggleStatusAsync(id);

        return Ok(new
        {
            Message = "Student status updated successfully"
        });
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery]
    UserStatisticsRequest request)
    {
        var result =
            await _service
                .GetStatisticsAsync(request);

        return Ok(result);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
    [FromQuery] StudentQueryRequest request)
    {
        request.Page = 1;
        request.PageSize = 100000;

        var result = await _service.SearchAsync(request);

        var lines = new List<string>
        {
            "StudentCode,NameAr,NameEn,NationalId,Email,Phone,Faculty,Program,Level,Status,PasswordStatus"
        };

        foreach (var student in result.Items)
        {
            string nameAr = "", nameEn = "";
            if (!string.IsNullOrEmpty(student.Name))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(student.Name);
                    nameAr = dict?.GetValueOrDefault("ar") ?? "";
                    nameEn = dict?.GetValueOrDefault("en") ?? "";
                }
                catch { }
            }

            lines.Add(
                $"{student.StudentCode}," +
                $"{EscapeCsv(nameAr)}," +
                $"{EscapeCsv(nameEn)}," +
                $"{student.NationalId}," +
                $"{student.Email}," +
                $"{student.PhoneNumber}," +
                $"{student.FacultyName}," +
                $"{student.ProgramName}," +
                $"{student.LevelName}," +
                $"{(student.IsActive ? "Active" : "Inactive")}," +
                $"{student.PasswordStatus}");
        }

        var csv = string.Join(
            Environment.NewLine,
            lines);

        var bytes =
            System.Text.Encoding.UTF8.GetBytes(csv);

        return File(
            bytes,
            "text/csv",
            $"students-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport(
        [FromBody] List<CreateStudentRequest> requests)
    {
        var createdIds = new List<Guid>();

        foreach (var request in requests)
        {
            var id = await _service.CreateAsync(request);

            createdIds.Add(id);
        }

        return Ok(new
        {
            Count = createdIds.Count,
            Message = "Students imported successfully"
        });
    }

    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportExcel(
    [FromQuery] StudentQueryRequest request)
    {
        request.Page = 1;
        request.PageSize = 100000;

        var result = await _service.SearchAsync(request);

        using var workbook = new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add("Students");

        worksheet.Cell(1, 1).Value = "Student Code";
        worksheet.Cell(1, 2).Value = "Name (Arabic)";
        worksheet.Cell(1, 3).Value = "Name (English)";
        worksheet.Cell(1, 4).Value = "National ID";
        worksheet.Cell(1, 5).Value = "Email";
        worksheet.Cell(1, 6).Value = "Phone";
        worksheet.Cell(1, 7).Value = "Faculty";
        worksheet.Cell(1, 8).Value = "Program";
        worksheet.Cell(1, 9).Value = "Level";
        worksheet.Cell(1, 10).Value = "Status";
        worksheet.Cell(1, 11).Value = "Password Status";

        int row = 2;

        foreach (var student in result.Items)
        {
            string nameAr = "", nameEn = "";
            if (!string.IsNullOrEmpty(student.Name))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(student.Name);
                    nameAr = dict?.GetValueOrDefault("ar") ?? "";
                    nameEn = dict?.GetValueOrDefault("en") ?? "";
                }
                catch { }
            }

            worksheet.Cell(row, 1).Value = student.StudentCode;
            worksheet.Cell(row, 2).Value = nameAr;
            worksheet.Cell(row, 3).Value = nameEn;
            worksheet.Cell(row, 4).Value = student.NationalId;
            worksheet.Cell(row, 5).Value = student.Email;
            worksheet.Cell(row, 6).Value = student.PhoneNumber;
            worksheet.Cell(row, 7).Value = student.FacultyName;
            worksheet.Cell(row, 8).Value = student.ProgramName;
            worksheet.Cell(row, 9).Value = student.LevelName;
            worksheet.Cell(row, 10).Value = student.IsActive ? "Active" : "Inactive";
            worksheet.Cell(row, 11).Value = student.PasswordStatus;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"students-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    [HttpPost("import-excel")]
    public async Task<IActionResult> ImportExcel(
    IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required");
        }

        using var stream = new MemoryStream();

        await file.CopyToAsync(stream);

        using var workbook =
            new XLWorkbook(stream);

        var worksheet =
            workbook.Worksheet(1);

        var rows =
            worksheet.RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            var request = new CreateStudentRequest
            {
                StudentCode = row.Cell(1).GetString(),
                NameAr = row.Cell(2).GetString(),
                NameEn = row.Cell(3).GetString(),
                NationalId = row.Cell(4).GetString(),
                Email = row.Cell(5).GetString(),
                PhoneNumber = row.Cell(6).GetString(),
                Password = row.Cell(7).GetString(),
                ConfirmPassword = row.Cell(7).GetString(),
                StructureNodeId = Guid.Parse(row.Cell(8).GetString())
            };

            await _service.CreateAsync(request);
        }

        return Ok(new
        {
            Message = "Students imported successfully"
        });
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\""))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}