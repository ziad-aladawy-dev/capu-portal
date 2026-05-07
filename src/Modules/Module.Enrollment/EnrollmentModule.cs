using CapitalUniversity.Core.Abstractions.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Module.Enrollment;

public class EnrollmentModule : IModule
{
    public void Register(IServiceCollection services)
    {
        // Register internal services
        // services.AddScoped<IEnrollmentService, EnrollmentService>();

        // Register ReadModels
        // The Enrollment module queries the StudentReadModel instead of direct Core DB access
        // services.AddScoped<IStudentReadModelQuery, StudentReadModelQuery>();
        // services.AddScoped<ICourseReadModelQuery, CourseReadModelQuery>();
    }
}
