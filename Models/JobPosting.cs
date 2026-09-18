namespace JobLedger.Models;

public class JobPosting
{
    public int Id { get; set; }
    public required string Title { get; set; }

    // Full job posting text. Stored here for future retrieval/matching against resume versions.
    public required string Content { get; set; }

    // Cover letter is stored on JobPosting rather than Application because it is
    // written specifically for this posting. Defaults to empty — you may not have
    // written one yet when the record is created.
    public string CoverLetter { get; set; } = "";

    // Foreign key linking this posting to its application.
    // JobPosting is owned by Application — it has no meaning outside of one.
    public int ApplicationId { get; set; }

    // Navigation property back to the owning application.
    // From here you can reach the company via Application.Company.
    public required Application Application { get; set; }
}