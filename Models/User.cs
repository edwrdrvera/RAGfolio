using Microsoft.AspNetCore.Identity;

namespace JobLedger.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; }
}