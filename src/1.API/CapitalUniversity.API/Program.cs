using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Infrastructure;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Configure JwtSettings & Options
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings?.Issuer,
            ValidAudience = jwtSettings?.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key ?? string.Empty))
        };
    });

// ProblemDetails & Exception Handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddDbContext<CoreDbContext>(options =>
        options
            .UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                // Transient SQL errors (deadlocks with retryable codes, brief
                // connection drops) re-execute the command without surfacing as
                // 500s. Six retries with EF's default exponential backoff is
                // the SqlServer provider default. No retryable exceptions are
                // added beyond the built-in list.
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));
}

builder.Services.AddCoreServices(builder.Configuration);

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

builder.Services.AddOptions<SessionVersionOptions>()
    .Bind(builder.Configuration.GetSection(SessionVersionOptions.SectionName));

builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires an authenticated principal by default. Login, refresh
    // (anon — caller carries an expired/expiring token), health, and swagger opt out
    // with [AllowAnonymous] or anonymous mappings below.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    // Apply pending EF migrations on startup when running against a relational
    // provider (skipped for InMemory which uses EnsureCreated in tests). Lets the
    // app pick up schema changes shipped in the same release without an explicit
    // "dotnet ef database update" step. Disable by setting
    // "Database:AutoMigrate" = false in appsettings if you prefer explicit gating.
    var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", true);
    if (autoMigrate && db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }

    await DataSeeder.SeedAsync(db, passwordHasher);
    await UniversityStructureSeeder.SeedAsync(db);
    await IdentitySeeder.SeedAsync(db);

    // Reconcile manifest-declared permissions against the DB. Additive only —
    // every module owns its permissions through IPermissionManifest, and the
    // synchroniser fills in any missing Module/Service rows without touching
    // teammate-seeded ones. Safe on every startup.
    var manifestSync = scope.ServiceProvider
        .GetRequiredService<CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest.IPermissionManifestSynchronizer>();
    await manifestSync.SynchronizeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<SessionVersionMiddleware>();
app.UseAuthorization();

// Health endpoint (anonymous).
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapControllers();
app.Run();

// Required by WebApplicationFactory<TEntryPoint> in integration tests.
public partial class Program { }