using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Courses.Configuration;
using CapitalUniversity.Sync.Courses.Persistence;
using CapitalUniversity.Sync.Courses.Pull;
using CapitalUniversity.Sync.Courses.Push;
using CapitalUniversity.Sync.Courses.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Courses.DependencyInjection;

public static class CoursesSyncServiceCollectionExtensions
{
    public static IServiceCollection AddCoursesSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<CoursesSyncOptions>, CoursesSyncOptionsValidator>();

        services
            .AddOptions<CoursesSyncOptions>()
            .Bind(configuration.GetSection(CoursesSyncOptions.SectionName))
            .ValidateOnStart();

        var options = configuration.GetSection(CoursesSyncOptions.SectionName).Get<CoursesSyncOptions>()
            ?? new CoursesSyncOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"{CoursesSyncOptions.SectionName}:ConnectionString is required.");
        }

        services.AddDbContext<CoursesSyncDbContext>(opts =>
            opts.UseSqlServer(
                options.ConnectionString,
                sql => sql.MigrationsHistoryTable(
                    "__CoursesSyncMigrationsHistory",
                    CoursesSyncDbContext.SchemaName)));

        services.AddSingleton<IExternalCourseSource, InMemoryExternalCourseSource>();

        services.AddSingleton<InMemoryExternalCourseSink>();
        services.AddSingleton<IExternalCourseSink>(sp =>
            sp.GetRequiredService<InMemoryExternalCourseSink>());

        services.AddTransient<CourseExtractor>();
        services.AddTransient<CourseMapper>();
        services.AddTransient<CourseValidator>();
        services.AddTransient<CourseWriter>();

        services.AddTransient<CourseOutboxExtractor>();
        services.AddTransient<CourseOutboxMapper>();
        services.AddTransient<CourseOutboxValidator>();
        services.AddTransient<CourseOutboxWriter>();

        services.AddSingleton<ISyncModule, CoursesSyncModule>();

        return services;
    }
}
