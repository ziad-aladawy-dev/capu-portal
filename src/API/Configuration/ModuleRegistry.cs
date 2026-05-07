using CapitalUniversity.Core.Abstractions.Modules;
// using CapitalUniversity.Module.Student;
using CapitalUniversity.Module.Enrollment;
using CapitalUniversity.Module.Complaints;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.API.Configuration;

public static class ModuleRegistry
{
    public static void RegisterModules(IServiceCollection services)
    {
        // Explicit registration only. No reflection, scanning, or runtime discovery.
        // new StudentModule().Register(services);
        new EnrollmentModule().Register(services);
        new ComplaintsModule().Register(services);
    }
}
