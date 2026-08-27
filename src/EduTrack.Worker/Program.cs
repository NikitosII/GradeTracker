using EduTrack.Application;
using EduTrack.Application.Notifications;
using EduTrack.Infrastructure.Messaging;
using EduTrack.Infrastructure.Persistence;
using EduTrack.Infrastructure.Scheduling;
using EduTrack.Infrastructure.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();

// The web process owns migrations/seeding; the worker only reads/writes.
builder.Services.AddPersistenceInfrastructure(builder.Configuration, applyMigrations: false);

builder.Services.AddTelegramSender(builder.Configuration);
builder.Services.AddMessagingInfrastructure(builder.Configuration);
builder.Services.AddSchedulingInfrastructure(builder.Configuration);

builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));

var host = builder.Build();
host.Run();
