using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
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
    [HasPermission(PermissionNames.Staff.View)]
    public async Task<IActionResult> Search([FromQuery] StaffQueryRequest request)
    {
        var result = await _service.SearchAsync(request);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [HasPermission(PermissionNames.Staff.View)]
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
    [HasPermission(PermissionNames.Staff.Insert)]
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
    [HasPermission(PermissionNames.Staff.EditClose)]
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
    [HasPermission(PermissionNames.Staff.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);

        return Ok(new
        {
            Message = "Staff deleted successfully"
        });
    }

    [HttpPatch("{id}/toggle-status")]
    [HasPermission(PermissionNames.Staff.EditClose)]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        await _service.ToggleStatusAsync(id);

        return Ok(new
        {
            Message = "Staff status updated successfully"
        });
    }

    [HttpGet("statistics")]
    [HasPermission(PermissionNames.Staff.View)]
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
    [HasPermission(PermissionNames.Staff.View)]
    public async Task<IActionResult> ExportCsv([FromQuery] StaffQueryRequest request)
    {
        const int chunkSize = 1000;
        request.Page = 1;
        request.PageSize = chunkSize;

        var firstPage = await _service.SearchAsync(request);
        var totalPages = firstPage.TotalPages;

        var sb = new System.Text.StringBuilder(firstPage.TotalCount * 128 + 200);
        sb.AppendLine("EmployeeCode,NameAr,NameEn,NationalId,Email,Phone,Role,JobTitle,Faculty,Status,PasswordStatus");

        for (var page = 1; page <= totalPages; page++)
        {
            var pageResult = page == 1
                ? firstPage
                : await _service.SearchAsync(new StaffQueryRequest
                {
                    ScopeNodeId = request.ScopeNodeId,
                    Search = request.Search,
                    IsActive = request.IsActive,
                    Role = request.Role,
                    JobTitle = request.JobTitle,
                    StructureNodeId = request.StructureNodeId,
                    Page = page,
                    PageSize = chunkSize
                });

            foreach (var staff in pageResult.Items)
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

                sb.Append(staff.EmployeeCode);
                sb.Append(',').Append(EscapeCsv(nameAr));
                sb.Append(',').Append(EscapeCsv(nameEn));
                sb.Append(',').Append(staff.NationalId);
                sb.Append(',').Append(staff.Email);
                sb.Append(',').Append(staff.PhoneNumber);
                sb.Append(',').Append(staff.Role);
                sb.Append(',').Append(staff.JobTitle);
                sb.Append(',').Append(staff.FacultyName);
                sb.Append(',').Append(staff.IsActive ? "Active" : "Inactive");
                sb.Append(',').AppendLine(staff.PasswordStatus);
            }
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());

        return File(
            bytes,
            "text/csv",
            $"staff-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("{id}/photo")]
    [HasPermission(PermissionNames.Staff.EditClose)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Message = "File is required" });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { Message = "Only JPEG, PNG and WebP images are allowed" });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { Message = "File size must be less than 5MB" });

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "photos");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"staff_{id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var photoUrl = $"/uploads/photos/{fileName}";
        await _service.UpdatePhotoAsync(id, photoUrl);

        return Ok(new { PhotoUrl = photoUrl, Message = "Photo uploaded successfully" });
    }

    [HttpPost("bulk-import")]
    [HasPermission(PermissionNames.Staff.Insert)]
    public async Task<IActionResult> BulkImport(
        [FromBody] List<CreateStaffRequest> requests)
    {
        var createdIds = await _service.BulkCreateAsync(requests);

        return Ok(new
        {
            Count = createdIds.Count,
            Message = "Staff imported successfully"
        });
    }

    [HttpGet("export-excel")]
    [HasPermission(PermissionNames.Staff.View)]
    public async Task<IActionResult> ExportExcel(
    [FromQuery] StaffQueryRequest request)
    {
        const int chunkSize = 1000;
        request.Page = 1;
        request.PageSize = chunkSize;

        var firstPage = await _service.SearchAsync(request);
        var totalPages = firstPage.TotalPages;

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

        for (var page = 1; page <= totalPages; page++)
        {
            var pageResult = page == 1
                ? firstPage
                : await _service.SearchAsync(new StaffQueryRequest
                {
                    ScopeNodeId = request.ScopeNodeId,
                    Search = request.Search,
                    IsActive = request.IsActive,
                    Role = request.Role,
                    JobTitle = request.JobTitle,
                    StructureNodeId = request.StructureNodeId,
                    Page = page,
                    PageSize = chunkSize
                });

            foreach (var staff in pageResult.Items)
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
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"staff-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    [HttpPost("import-excel")]
    [HasPermission(PermissionNames.Staff.Insert)]
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

        using (var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
        {
            foreach (var row in rows)
            {
                var request = new CreateStaffRequest
                {
                    EmployeeCode = row.Cell(1).GetString(),
                    NameAr = row.Cell(2).GetString(),
                    NameEn = row.Cell(3).GetString(),
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
            transaction.Complete();
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
