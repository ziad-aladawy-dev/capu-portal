using CapitalUniversity.Core.Abstractions.Semesters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Core.Infrastructure.Services.Semesters;

public class AcademicTimelineBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AcademicTimelineBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public AcademicTimelineBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AcademicTimelineBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Academic Timeline Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResolveTimelineAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resolving academic timeline.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Academic Timeline Background Service is stopping.");
    }

    private async Task ResolveTimelineAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var yearService = scope.ServiceProvider.GetRequiredService<IAcademicYearService>();
        var semesterService = scope.ServiceProvider.GetRequiredService<ISemesterService>();

        _logger.LogInformation("Resolving current academic year and semester...");

        await yearService.ResolveCurrentYearAsync();
        await semesterService.ResolveCurrentSemesterAsync();

        _logger.LogInformation("Academic timeline resolution completed.");
    }
}
