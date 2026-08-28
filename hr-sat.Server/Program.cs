using hr_sat.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.MapGet("/api/vacancies", async (AppDbContext db) =>
    await db.Vacancies
        .Select(v => new VacancySummary(v.Id, v.Title, v.CreatedOn))
        .ToListAsync());

app.Run();

public sealed record VacancySummary(Guid Id, string Title, DateTime CreatedOn);

// Exposes the implicit Program class to WebApplicationFactory in integration tests.
public partial class Program;

