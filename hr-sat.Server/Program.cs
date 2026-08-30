using hr_sat.Server.Features.Candidates;
using hr_sat.Server.Features.Vacancies;
using hr_sat.Server.Infrastructure;
using hr_sat.Server.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.Configure<PrivateFileStorageOptions>(
    builder.Configuration.GetSection(PrivateFileStorageOptions.SectionName));
builder.Services.AddSingleton<IPrivateFileStorage, PrivateFileStorage>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.SeedAsync();
}

app.MapVacancyEndpoints();
app.MapCandidateEndpoints();

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in integration tests.
public partial class Program;

