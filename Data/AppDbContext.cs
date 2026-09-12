using Microsoft.EntityFrameworkCore;
using JobLedger.Models;

namespace JobLedger.Data;

// DbContext is the bridge between your C# models and the database.
// It manages the connection, tracks changes, and exposes DbSet properties for querying.
public class JobLedgerDbContext : DbContext
{
    // Accepts EF Core configuration (connection string, provider) via DI.
    // Passes it up to the base DbContext constructor.
    public JobLedgerDbContext(DbContextOptions<JobLedgerDbContext> options)
    : base(options) 
    {
        
    }

    // Each DbSet maps to a table in the database.
    // EF Core uses these to generate SQL for queries, inserts, updates, and deletes.
    public DbSet<Company> Companies { get; set; }
    public DbSet<Application> Applications { get; set; }
}