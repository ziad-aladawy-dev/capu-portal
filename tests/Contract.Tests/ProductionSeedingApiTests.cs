using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// B3 — production seeding posture: with Seeding:DemoData=false the demo accounts
/// (built-in admin + students) are NOT seeded, and a Super Admin is bootstrapped
/// instead from Seeding:Admin:* configuration. Proves the config admin can log in
/// and reach admin surface, while a demo student cannot authenticate.
/// </summary>
public class ProductionSeedingApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private const string AdminNid = "99900011122233";
    private const string AdminPwd = "Str0ng!AdminPwd";
    private const string DemoStudentNid = "30201011234567"; // seeded only when demo is on

    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public ProductionSeedingApiTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "ProdSeedingDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seeding:DemoData"] = "false",
                    ["Seeding:Admin:NationalId"] = AdminNid,
                    ["Seeding:Admin:Password"] = AdminPwd,
                    ["Seeding:Admin:Name"] = "Prod Admin",
                    ["Seeding:Admin:Email"] = "prodadmin@capital.local",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddScoped(_ => new Mock<IAppLogger>().Object);
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });
    }

    [Fact]
    public async Task ConfigAdmin_CanLogIn_AndReachAdminSurface()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = AdminNid, Password = AdminPwd });
        login.StatusCode.Should().Be(HttpStatusCode.OK,
            "the config-driven Super Admin must be bootstrapped when demo data is off");

        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);

        // Super Admin holds grant-all, so an admin endpoint is reachable.
        var permissions = await client.GetAsync("/api/permissions");
        permissions.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DemoAccounts_AreNotSeeded_WhenDemoDataOff()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = DemoStudentNid, Password = "123456" });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "demo students must not exist when Seeding:DemoData is false");
    }
}
