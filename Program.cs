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
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Adds Swagger so we can test endpoints in the browser at /swagger
builder.Services.AddOpenApi();

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

app.MapGet("/", () => "Hello World!");

app.MapOpenApi();

// Returns all applications from the database.
// .Include(a => a.Company) also loads the related Company for each application,
// otherwise the Company field would come back as null.
app.MapGet("/applications", async (JobLedgerDbContext db) =>
{
    var applications = await db.Applications.Include(a => a.Company).ToListAsync();

    return Results.Ok(applications);
});

// GET /applications/{id} — returns a single application by id with its related Company loaded
// Returns 404 if no application with that id exists
app.MapGet("/applications/{id}", async (JobLedgerDbContext db, int id) =>
{
    // FindAsync doesn't support .Include(), so we use FirstOrDefaultAsync instead
    // FirstOrDefaultAsync returns null if no match is found
    var application = await db.Applications
    .Include(a => a.Company)
    .FirstOrDefaultAsync(a => a.Id == id);

    if (application == null)
    {
        return Results.NotFound();
    }
    return Results.Ok(application);
});


// POST /applications — creates a new application from a DTO
// Accepts a CompanyName string instead of a full Company object;
// looks up the company by name or creates a new one if it doesn't exist
app.MapPost("/applications", async (JobLedgerDbContext db, CreateApplicationDto dto) =>
{
    // Find existing company or create a new one
    var company = await db.Companies
        .FirstOrDefaultAsync(c => c.Name == dto.CompanyName)
        ?? new Company { Name = dto.CompanyName };

    // Build the Application entity from the DTO fields.
    // The caller sent a company name string, but the entity needs a full Company object —
    // this is where that conversion happens.
    var application = new Application
    {
        Company = company,
        Role = dto.Role,
        Status = dto.Status
    };

    db.Applications.Add(application);
    await db.SaveChangesAsync();
    return Results.Created($"/applications/{application.Id}", application);

}).RequireAuthorization("OwnerOnly");

// DELETE /applications/{id} — removes an application by id
// Returns 404 if not found, 204 No Content on success (nothing to send back after a delete)
app.MapDelete("/applications/{id}", async (JobLedgerDbContext db, int id) =>
{
    var application = await db.Applications.FindAsync(id);

    if (application == null)
    {
        return Results.NotFound();
    }

    db.Applications.Remove(application);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("OwnerOnly");

// PUT /applications/{id} — replaces all fields on an existing application
app.MapPut("/applications/{id}", async (JobLedgerDbContext db, int id, UpdateApplicationDto dto) =>
{
    var application = await db.Applications
        .Include(a => a.Company)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (application == null)
    {
        return Results.NotFound();
    }

    // Find existing company or create a new one
    var company = await db.Companies
        .FirstOrDefaultAsync(c => c.Name == dto.CompanyName)
        ?? new Company { Name = dto.CompanyName };

    application.Company = company;
    application.Role = dto.Role;
    application.Status = dto.Status;

    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization("OwnerOnly");

// GET /applications/stale?days=X — returns applications that haven't been updated in more than X days
app.MapGet("/applications/stale", async (JobLedgerDbContext db, int days) =>
{
    var applications = await db.Applications
    .Where(a => a.UpdatedAt < DateTime.UtcNow.AddDays(-days))
    .ToListAsync();

    return Results.Ok(applications);
});

// GET /resumeversions/{id}/usage-count — returns how many applications a resume version has been used on
app.MapGet("/resumeversions/{id}/usage-count", async (JobLedgerDbContext db, int id) =>
{
    var resumeVersion = await db.ResumeVersions
        .Include(a => a.Applications)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (resumeVersion == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(resumeVersion.Applications.Count);
});

// POST /auth/register — creates a new user with a hashed password; rejects duplicate usernames
app.MapPost("/auth/register", async (JobLedgerDbContext db, AuthDto dto) =>
{
    // Find existing user or create a new one
    var existingUser = await db.Users
        .FirstOrDefaultAsync(user => user.Username == dto.Username);

    if (existingUser != null)
    {
        return Results.BadRequest("Username is already taken.");
    }
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
});

// POST /auth/login — verifies credentials and returns a signed JWT on success
app.MapPost("auth/login", async (JobLedgerDbContext db, AuthDto dto) =>
{
    // Find the user
    var existingUser = await db.Users
    .FirstOrDefaultAsync(a => a.Username == dto.Username);

    // User not found - 401
    if (existingUser == null)
    {
        return Results.Unauthorized();
    }

    // Verify the password
    var hasher = new PasswordHasher<User>();
    var result = hasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, dto.Password);

    if (result == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    // Initialize token handler
    var tokenHandler = new JwtSecurityTokenHandler();
    // Converts secret key to bytes
    var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);

    // Blueprint for token creation
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

    // Claims, expiry, and signing key for the token
    var token = tokenHandler.CreateToken(tokenDescriptor);
    // Serialize token to xxxxx.yyyyy.zzzzz string
    var tokenString = tokenHandler.WriteToken(token);

    return Results.Ok(new { token = tokenString });

});

app.Run();


