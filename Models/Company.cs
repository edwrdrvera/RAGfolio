namespace JobLedger.Models;

public class Company
{
    public int Id { get; set; }
    public required string Name { get; set; }

    // Form a bi-directional relationship with Applications so that Company has a list of corresponding Applications
    public List<Application> Applications { get; set; } = [];
}