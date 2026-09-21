using System.Text.Json;
using System.Text.Json.Serialization;
using hr_sat.Application;
using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Dispatching;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Infrastructure;
using hr_sat.Infrastructure.Dispatching;
using hr_sat.Infrastructure.Email;
using hr_sat.Infrastructure.Storage;
using hr_sat.Web.Api;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new ScreeningOperatorJsonConverter());
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services.AddApplication();
builder.Services.AddEndpoints();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
var connectionString = builder.Configuration.GetConnectionString("Database");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IApplicationDbContext>(
    services => services.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<ICandidateListReader, PostgresCandidateListReader>();
builder.Services.Configure<PrivateFileStorageOptions>(
    builder.Configuration.GetSection(PrivateFileStorageOptions.SectionName));
builder.Services.AddSingleton<IPrivateFileStorage, PrivateFileStorage>();
builder.Services.AddHostedService<FileDeletionSweeper>();
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));
// Data Protection protects the saved SMTP Account password at rest (purpose
// smtp-settings-v1, issue 02 decision 1); keys persist under the installing user's
// profile, so a restart never strands the saved row.
builder.Services.AddDataProtection();
builder.Services.AddScoped<SmtpSettingsStore>();
builder.Services.AddScoped<ISmtpSettingsStore>(services =>
    services.GetRequiredService<SmtpSettingsStore>());
builder.Services.AddScoped<ISmtpConnectionTester, MailKitSmtpConnectionTester>();
// The Microsoft Account sign-in seam (issue 03): one singleton MSAL public client per
// process — the token cache and the in-flight connect attempt live on it.
builder.Services.AddSingleton<IMicrosoftAccountSignIn, MicrosoftAccountSignIn>();
// Scoped, resolving the effective account through the settings store once per scope —
// a run pins one account (issue 02 decision 13); a save takes effect without restart.
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
builder.Services.AddScoped<IDispatchRunLock>(
    _ => new PostgresDispatchRunLock(connectionString!));
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var fileStorage = scope.ServiceProvider.GetRequiredService<IPrivateFileStorage>();
    var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
    await dbContext.Database.MigrateAsync();
    await dbContext.SeedAsync(fileStorage, timeProvider);
}

app.MapEndpoints();

app.Run();

