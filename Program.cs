var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

var applications = new List<Application> { new(1, "Company A", "Developer", "Applied")};

app.MapGet("/applications", () => applications);

app.MapPost("/applications", (Application application) =>
{
    applications.Add(application);
    return Results.Created($"/applications/{application.Id}", application);
}); 

app.Run();

internal record Application(int Id, string Company, string Role, string Status);



