namespace JobLedger.Models;

public enum Status { Applied, Interviewing, Rejected, Offer };
public class Application
{
    public int Id { get; set; }
    public required int CompanyId { get; set; }
    public required Company Company { get; set; }
    public required string Role { get; set; }

    // Creates an enum property Status
    public required Status Status { get; set; } = Status.Applied;
    
}