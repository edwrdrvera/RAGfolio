using System.Net;
using System.Net.Http.Json;
using JobLedger.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace JobLedger.Tests;

// A custom factory that overrides how the app boots during tests.
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Shared in-memory store so every request in every test sees the same data.
    // Without this, EnableServiceProviderCaching(false) would give each DbContext
    // its own separate store, so register and login would not share data.
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Points the factory at the actual project root folder.
        // Needed because the .sln and .csproj live in the same directory.
        builder.UseContentRoot(Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));

        // Provides fake JWT settings so the app doesn't crash during tests.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-that-is-long-enough-32chars",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            });
        });

        // ConfigureTestServices runs AFTER Program.cs registers all its services,
        // so our changes here override what Program.cs set up.
        builder.ConfigureTestServices(services =>
        {
            // Remove all four registrations that AddDbContext creates for Npgsql.
            services.RemoveAll<JobLedgerDbContext>();
            services.RemoveAll<DbContextOptions<JobLedgerDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<JobLedgerDbContext>>();

            // Register a fake in-memory database instead.
            // Passing _dbRoot ensures all requests share the same in-memory store.
            services.AddDbContext<JobLedgerDbContext>(options =>
                options.UseInMemoryDatabase("TestDb", _dbRoot)
                       .EnableServiceProviderCaching(false));
        });
    }
}

// Tells xUnit to create the factory one time and reuse it for every test in this class.
public class ApplicationsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    // HTTP client we use to send requests to our API during tests.
    private readonly HttpClient _client;

    // Runs once before any tests in this class start.
    // xUnit automatically passes in the factory.
    public ApplicationsEndpointTests(TestWebApplicationFactory factory)
    {
        // The database is shared across tests, so wipe it before each one runs.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobLedgerDbContext>();
            db.Database.EnsureDeleted();
        }

        // Boots the app and returns an HTTP client pointed at it.
        // The InMemory database creates its tables automatically on first use.
        _client = factory.CreateClient();
    }

    // Marks this method as a test. xUnit finds and runs it automatically.
    [Fact]
    public async Task GetApplications_ReturnsOk()
    {
        // Send a GET request to /applications
        var response = await _client.GetAsync("/applications");

        // Checks that we got a 200 OK back.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostApplication_WithoutToken_ReturnsUnauthorized()
    {
        // Sends a POST request with no login token attached.
        var response = await _client.PostAsJsonAsync("/applications", new
        {
            CompanyName = "Google",
            Role = "Engineer",
            Status = "Applied"
        });

        // POST /applications requires the Owner role, so we expect 401 Unauthorized back.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostApplication_WithToken_ReturnsCreated()
    {
        // Register user
        await _client.PostAsJsonAsync("/auth/register", new
        {
            Username = "test",
            Password = "password123"
        });

        // Login user
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
        {
            Username = "test",
            Password = "password123"
        });

        // Extract the token string from the JSON response
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var token = loginResult!["token"];

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/applications", new
        {
            CompanyName = "Google",
            Role = "Software Engineer",
            Status = "Applied"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
