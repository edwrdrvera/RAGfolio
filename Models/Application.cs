namespace JobLedger.Models;

public class Application
{
    public int Id { get; set; }
    public required int CompanyId { get; set; }
    public required Company Company { get; set; }
    public required string Role { get; set; }
    public required string Status { get; set; }
}