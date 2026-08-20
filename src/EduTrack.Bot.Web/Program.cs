using EduTrack.Bot.Web.Telegram;
using EduTrack.Infrastructure.Persistence;
using EduTrack.Infrastructure.Telegram;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistenceInfrastructure(builder.Configuration);
builder.Services.AddTelegramInfrastructure(builder.Configuration);

builder.Services.AddScoped<WebhookUpdateProcessor>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

var app = builder.Build();

app.MapTelegramWebhook();

app.MapHealthChecks("/health/live", new()
{
    Predicate = check => check.Tags.Contains("live"),
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapGet("/", () => "EduTrack GradeTracker Bot is running.");

app.Run();

public partial class Program;
