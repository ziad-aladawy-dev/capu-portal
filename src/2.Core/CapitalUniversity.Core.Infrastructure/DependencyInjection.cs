using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.UniversityStructure;
using CapitalUniversity.Core.Application.UniversityStructure;
using CapitalUniversity.Core.Infrastructure.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services)
    {
        services.AddScoped<IStructureNodeRepository,StructureNodeRepository>();

        services.AddScoped<IUniversityStructureService,UniversityStructureService>();

        return services;
    }
}