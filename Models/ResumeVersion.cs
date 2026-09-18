namespace JobLedger.Models;

public class ResumeVersion
{
    public int Id { get; set; }

    // A human-readable name for this version, e.g. "SWE v3" or "Backend focused".
    public required string Label { get; set; }

    // Full resume text. Stored here so a future retrieval layer can search it
    // without needing to parse external files.
    public required string Content { get; set; }

    // Many-to-many with Application. One resume version can be submitted across
    // multiple applications. EF Core sees a matching collection on Application
    // and auto-generates the join table — no explicit join entity needed.
    public List<Application> Applications { get; set; } = [];
}