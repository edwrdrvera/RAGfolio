using System.Text.Json.Serialization;
using JobLedger.Data;
using JobLedger.Models;
using Microsoft.EntityFrameworkCore;

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



var app = builder.Build();

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

});

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
});

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
});

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

app.Run();


