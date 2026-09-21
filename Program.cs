using System.Text;
using System.Text.Json.Serialization;
using JobLedger.Data;
using JobLedger.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "JobLedger API",
            Version = "v1",
            Description = "A job-search tracker. Tracks companies, applications, resume versions, and job postings."
        };

        // Declare the JWT bearer scheme so Scalar shows an Authorize button
        document.Components = new OpenApiComponents
        {
            SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste a token from POST /auth/login"
                }
            }
        };

        return Task.CompletedTask;
    });
});

// Registers the database context with DI so route handlers can receive it as a parameter
builder.Services.AddDbContext<JobLedgerDbContext>(
    options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application has a Company, and Company has a list of Applications back — a cycle.
// Without this, the JSON serializer would loop forever trying to serialize them.
// IgnoreCycles tells it to just stop when it hits something it already serialized.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Registers the auth and sets default scheme (default request handler is JWT Bearer)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Four checks to determine a token is valid
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Who issued it matches
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            // Who its for matches
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            // Is the token still valid
            ValidateLifetime = true,

            // Validates the token signature using the secret key; symmetric means the same key signs and verifies
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy =>
    {
        policy.RequireRole("Owner");
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!").ExcludeFromDescription();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/applications", async (JobLedgerDbContext db) =>
{
    var applications = await db.Applications.Include(a => a.Company).ToListAsync();
    return Results.Ok(applications);
})
.WithTags("Applications")
.WithSummary("List all applications")
.WithDescription("Returns every job application with its associated company.")
.Produces<List<Application>>(200);

app.MapGet("/applications/{id}", async (JobLedgerDbContext db, int id) =>
{
    var application = await db.Applications
    .Include(a => a.Company)
    .FirstOrDefaultAsync(a => a.Id == id);

    if (application == null) return Results.NotFound();
    return Results.Ok(application);
})
.WithTags("Applications")
.WithSummary("Get an application by id")
.Produces<Application>(200)
.Produces(404);

app.MapPost("/applications", async (JobLedgerDbContext db, CreateApplicationDto dto) =>
{
    var company = await db.Companies
        .FirstOrDefaultAsync(c => c.Name == dto.CompanyName)
        ?? new Company { Name = dto.CompanyName };

    var application = new Application
    {
        Company = company,
        Role = dto.Role,
        Status = dto.Status
    };

    db.Applications.Add(application);
    await db.SaveChangesAsync();
    return Results.Created($"/applications/{application.Id}", application);
})
.WithTags("Applications")
.WithSummary("Create an application")
.WithDescription("Creates a new application. Looks up the company by name; creates it if it doesn't exist. Requires the Owner role.")
.Produces<Application>(201)
.RequireAuthorization("OwnerOnly");

app.MapDelete("/applications/{id}", async (JobLedgerDbContext db, int id) =>
{
    var application = await db.Applications.FindAsync(id);

    if (application == null) return Results.NotFound();

    db.Applications.Remove(application);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithTags("Applications")
.WithSummary("Delete an application")
.Produces(204)
.Produces(404)
.RequireAuthorization("OwnerOnly");

app.MapPut("/applications/{id}", async (JobLedgerDbContext db, int id, UpdateApplicationDto dto) =>
{
    var application = await db.Applications
        .Include(a => a.Company)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (application == null) return Results.NotFound();

    var company = await db.Companies
        .FirstOrDefaultAsync(c => c.Name == dto.CompanyName)
        ?? new Company { Name = dto.CompanyName };

    application.Company = company;
    application.Role = dto.Role;
    application.Status = dto.Status;

    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithTags("Applications")
.WithSummary("Update an application")
.WithDescription("Replaces all fields on an existing application. Requires the Owner role.")
.Produces(204)
.Produces(404)
.RequireAuthorization("OwnerOnly");

app.MapGet("/applications/stale", async (JobLedgerDbContext db, int days) =>
{
    var applications = await db.Applications
    .Where(a => a.UpdatedAt < DateTime.UtcNow.AddDays(-days))
    .ToListAsync();

    return Results.Ok(applications);
})
.WithTags("Applications")
.WithSummary("List stale applications")
.WithDescription("Returns applications that have not been updated in more than the given number of days.")
.Produces<List<Application>>(200);

app.MapGet("/resumeversions/{id}/usage-count", async (JobLedgerDbContext db, int id) =>
{
    var resumeVersion = await db.ResumeVersions
        .Include(a => a.Applications)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (resumeVersion == null) return Results.NotFound();

    return Results.Ok(resumeVersion.Applications.Count);
})
.WithTags("Resume Versions")
.WithSummary("Get usage count for a resume version")
.WithDescription("Returns the number of applications that reference this resume version.")
.Produces<int>(200)
.Produces(404);

app.MapPost("/auth/register", async (JobLedgerDbContext db, AuthDto dto) =>
{
    var existingUser = await db.Users
        .FirstOrDefaultAsync(user => user.Username == dto.Username);

    if (existingUser != null) return Results.BadRequest("Username is already taken.");

    var hasher = new PasswordHasher<User>();

    var newUser = new User
    {
        Username = dto.Username,
        PasswordHash = hasher.HashPassword(new User(), dto.Password),
        Role = UserRole.Owner
    };

    db.Users.Add(newUser);
    await db.SaveChangesAsync();
    return Results.Ok("User registered successfully.");
})
.WithTags("Auth")
.WithSummary("Register a new user")
.WithDescription("Creates a new user with a hashed password. Rejects duplicate usernames.")
.Produces<string>(200)
.Produces(400);

app.MapPost("/auth/login", async (JobLedgerDbContext db, AuthDto dto) =>
{
    var existingUser = await db.Users
    .FirstOrDefaultAsync(a => a.Username == dto.Username);

    if (existingUser == null) return Results.Unauthorized();

    var hasher = new PasswordHasher<User>();
    var result = hasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, dto.Password);

    if (result == PasswordVerificationResult.Failed) return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, existingUser.Username), new Claim(ClaimTypes.Role, existingUser.Role.ToString())]),
        Expires = DateTime.UtcNow.AddHours(1),
        Issuer = builder.Configuration["Jwt:Issuer"],
        Audience = builder.Configuration["Jwt:Audience"],
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature
        )
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);

    return Results.Ok(new { token = tokenString });
})
.WithTags("Auth")
.WithSummary("Login")
.WithDescription("Verifies credentials and returns a signed JWT valid for 1 hour. Use the token in the Authorization header as `Bearer <token>`.")
.Produces(200)
.Produces(401);

app.Run();
