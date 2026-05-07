using CapitalUniversity.Core.Abstractions.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Module.Complaints;

public class ComplaintsModule : IModule
{
    public void Register(IServiceCollection services)
    {
        // Register internal services
        // services.AddScoped<IComplaintService, ComplaintService>();

        // Register ReadModels
        // The Complaints module uses decoupled ReadModels
        // services.AddScoped<IStudentReadModelQuery, StudentReadModelQuery>();
        // services.AddScoped<IStaffReadModelQuery, StaffReadModelQuery>();
    }
}
