using Microsoft.EntityFrameworkCore;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.API.Configuration;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<CoreDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Add services to the container.
        ModuleRegistry.RegisterModules(builder.Services);
        builder.Services.AddAuthServices();

        builder.Services.AddControllers();

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
