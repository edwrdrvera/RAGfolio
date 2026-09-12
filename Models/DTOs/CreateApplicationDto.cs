using JobLedger.Models;

public class CreateApplicationDto
{
    public string CompanyName { get; set; } = "";
    public string Role { get; set; } = "";
    public Status Status { get; set; } = Status.Applied;
}