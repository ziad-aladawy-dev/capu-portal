using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.Api.Controllers;

[ApiController]
[Route("api/staff")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _service;

    public StaffController(IStaffService service)
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
    public async Task<IActionResult> Search([FromQuery] StaffQueryRequest request)
    {
        var result = await _service.SearchAsync(request);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (result == null)
        {
            return NotFound(new
            {
                Message = "Staff not found"
            });
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffRequest request)
    {
        var id = await _service
            .CreateAsync(request);

        return Ok(new
        {
            Id = id,
            Message = "Staff created successfully"
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStaffRequest request)
    {
        await _service.UpdateAsync(id, request);

        return Ok(new
        {
            Message = "Staff updated successfully"
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);

        return Ok(new
        {
            Message = "Staff deleted successfully"
        });
    }

    [HttpPatch("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        await _service.ToggleStatusAsync(id);

        return Ok(new
        {
            Message = "Staff status updated successfully"
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
    public async Task<IActionResult> ExportCsv([FromQuery] StaffQueryRequest request)
    {
        request.Page = 1;
        request.PageSize = 100000;

        var result = await _service.SearchAsync(request);

        var lines = new List<string>
        {
            "EmployeeCode,NameAr,NameEn,NationalId,Email,Phone,Role,JobTitle,Faculty,Status,PasswordStatus"
        };

        foreach (var staff in result.Items)
        {
            string nameAr = "", nameEn = "";
            if (!string.IsNullOrEmpty(staff.Name))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(staff.Name);
                    nameAr = dict?.GetValueOrDefault("ar") ?? "";
                    nameEn = dict?.GetValueOrDefault("en") ?? "";
                }
                catch { }
            }

            lines.Add(
                $"{staff.EmployeeCode}," +
                $"{EscapeCsv(nameAr)}," +
                $"{EscapeCsv(nameEn)}," +
                $"{staff.NationalId}," +
                $"{staff.Email}," +
                $"{staff.PhoneNumber}," +
                $"{staff.Role}," +
                $"{staff.JobTitle}," +
                $"{staff.FacultyName}," +
                $"{(staff.IsActive ? "Active" : "Inactive")}," +
                $"{staff.PasswordStatus}");
        }

        var csv = string.Join(Environment.NewLine, lines);

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

        return File(
            bytes,
            "text/csv",
            $"staff-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImport(
        [FromBody] List<CreateStaffRequest> requests)
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
            Message = "Staff imported successfully"
        });
    }

    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportExcel(
    [FromQuery] StaffQueryRequest request)
    {
        var result = await _service.SearchAsync(request);

        using var workbook = new XLWorkbook();

        var worksheet = workbook.Worksheets.Add("Staff");

        worksheet.Cell(1, 1).Value = "Employee Code";
        worksheet.Cell(1, 2).Value = "Name (Arabic)";
        worksheet.Cell(1, 3).Value = "Name (English)";
        worksheet.Cell(1, 4).Value = "National ID";
        worksheet.Cell(1, 5).Value = "Email";
        worksheet.Cell(1, 6).Value = "Phone";
        worksheet.Cell(1, 7).Value = "Role";
        worksheet.Cell(1, 8).Value = "Job Title";
        worksheet.Cell(1, 9).Value = "Faculty";
        worksheet.Cell(1, 10).Value = "Status";
        worksheet.Cell(1, 11).Value = "Password Status";

        int row = 2;

        foreach (var staff in result.Items)
        {
            string nameAr = "", nameEn = "";
            if (!string.IsNullOrEmpty(staff.Name))
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(staff.Name);
                    nameAr = dict?.GetValueOrDefault("ar") ?? "";
                    nameEn = dict?.GetValueOrDefault("en") ?? "";
                }
                catch { }
            }

            worksheet.Cell(row, 1).Value = staff.EmployeeCode;
            worksheet.Cell(row, 2).Value = nameAr;
            worksheet.Cell(row, 3).Value = nameEn;
            worksheet.Cell(row, 4).Value = staff.NationalId;
            worksheet.Cell(row, 5).Value = staff.Email;
            worksheet.Cell(row, 6).Value = staff.PhoneNumber;
            worksheet.Cell(row, 7).Value = staff.Role;
            worksheet.Cell(row, 8).Value = staff.JobTitle;
            worksheet.Cell(row, 9).Value = staff.FacultyName;
            worksheet.Cell(row, 10).Value = staff.IsActive ? "Active" : "Inactive";
            worksheet.Cell(row, 11).Value = staff.PasswordStatus;
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        var content = stream.ToArray();

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "staff.xlsx");
    }

    [HttpPost("import-excel")]
    public async Task<IActionResult> ImportExcel(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required");
        }

        using var stream = new MemoryStream();

        await file.CopyToAsync(stream);

        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheet(1);

        var rows = worksheet.RowsUsed().Skip(1);
        

        foreach (var row in rows)
        {
            var NameSerialized = System.Text.Json.JsonSerializer.Serialize(new
            {
                ar = row.Cell(2).GetString(),
                en = row.Cell(3).GetString()
            });
            var request = new CreateStaffRequest
            {
                EmployeeCode = row.Cell(1).GetString(),
                Name = NameSerialized,
                NationalId = row.Cell(4).GetString(),
                Email = row.Cell(5).GetString(),
                PhoneNumber = row.Cell(6).GetString(),
                Role = row.Cell(7).GetString(),
                JobTitle = row.Cell(8).GetString(),
                Password = row.Cell(9).GetString(),
                ConfirmPassword = row.Cell(9).GetString(),
                StructureNodeId = Guid.Parse(row.Cell(10).GetString())
            };

            await _service.CreateAsync(request);
        }

        return Ok(new
        {
            Message = "Staff imported successfully"
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