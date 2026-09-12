using System.Text.Json.Serialization;
using JobLedger.Data;
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
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

// Returns all applications from the database.
// .Include(a => a.Company) also loads the related Company for each application,
// otherwise the Company field would come back as null.
app.MapGet("/applications", async (JobLedgerDbContext db) => 
    await db.Applications.Include(a => a.Company).ToListAsync());

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

app.Run();


