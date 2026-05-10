using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.API.Configuration;
using CapitalUniversity.API.Middleware;
using CapitalUniversity.Core.Abstractions.Logging;
using CapitalUniversity.Core.Infrastructure.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure MongoDB settings
        builder.Services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoSettings"));

        // Register MongoClient as Singleton
        builder.Services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        // Register MongoLoggerService
        builder.Services.AddSingleton<IAppLogger, MongoLoggerService>();

        // Configure Forwarded Headers for IIS/Reverse Proxy
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
        });

        builder.Services.AddDbContext<CoreDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Add services to the container.
        builder.Services.AddScoped<CapitalUniversity.Core.Abstractions.Notifications.INotificationService, CapitalUniversity.Core.Application.Notifications.NotificationService>();

        ModuleRegistry.RegisterModules(builder.Services);

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Use Forwarded Headers
        app.UseForwardedHeaders();

        // Use Global Exception Middleware
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
