using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
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
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
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
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetStudentStatisticsById(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _statisticsService.GetStudentStatisticsAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("staff/recent-requests")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetRecentRequests([FromQuery] int count = 5, CancellationToken cancellationToken = default)
    {
        var result = await _statisticsService.GetRecentRequestsAsync(count, cancellationToken);
        return Ok(result);
    }

    [HttpGet("staff/trend")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetRequestTrend([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var result = await _statisticsService.GetRequestTrendAsync(days, cancellationToken);
        return Ok(result);
    }
}