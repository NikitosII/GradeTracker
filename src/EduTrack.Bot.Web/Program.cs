using EduTrack.Application;
using EduTrack.Bot.Web.Conversations;
using EduTrack.Bot.Web.Telegram;
using EduTrack.Infrastructure.Persistence;
using EduTrack.Infrastructure.Telegram;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddPersistenceInfrastructure(builder.Configuration);
builder.Services.AddTelegramInfrastructure(builder.Configuration);

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<IConversationStore, ConversationStore>();
builder.Services.AddScoped<GradeModule>();
builder.Services.AddScoped<DeadlineModule>();
builder.Services.AddScoped<AdminModule>();
builder.Services.AddScoped<ReminderModule>();
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
