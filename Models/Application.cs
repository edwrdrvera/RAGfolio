namespace JobLedger.Models;

public enum Status { Applied, Interviewing, Rejected, Offer };
public class Application
{
    public int Id { get; set; }

    // Foreign key column in the database. EF Core links this to the Company navigation property by convention.
    public int CompanyId { get; set; }

    // Navigation property — lets you traverse to the related Company object in C#.
    // required because every application must belong to a company.
    public required Company Company { get; set; }

    public required string Role { get; set; }
    public required Status Status { get; set; } = Status.Applied;

    // Many-to-many with ResumeVersion. One application can use multiple resume versions,
    // and one resume version can be used across multiple applications.
    // EF Core sees a collection on both sides and auto-generates a join table.
    public List<ResumeVersion> ResumeVersions { get; set; } = [];

    // One-to-one with JobPosting. Nullable because an application may be created
    // before a job posting is attached to it.
    public JobPosting? JobPosting { get; set; }
}