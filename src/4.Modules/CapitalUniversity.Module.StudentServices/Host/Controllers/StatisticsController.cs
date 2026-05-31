//using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.Module.StudentServices.Host.Controllers;

[ApiController]
[Route("api/student-services/statistics")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IDashboardStatisticsService _statisticsService;
    private readonly ICurrentUser _currentUser;

    public StatisticsController(IDashboardStatisticsService statisticsService, ICurrentUser currentUser)
    {
        _statisticsService = statisticsService;
        _currentUser = currentUser;
    }

    [HttpGet("staff")]
    //[HasPermission("student-services.services.View")]
    public async Task<IActionResult> GetStaffStatistics(CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetStaffStatisticsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("student/me")]
    public async Task<IActionResult> GetMyStudentStatistics(CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetStudentStatisticsAsync(_currentUser.Id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("student/{studentId:guid}")]
    //[HasPermission("student-services.statistics.ViewAny")]
    public async Task<IActionResult> GetStudentStatisticsById(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetStudentStatisticsAsync(studentId, cancellationToken);
        return Ok(result);
    }
}