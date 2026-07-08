using HoshiBot.Data;
using HoshiBot.Discord;
using HoshiBot.Discord.Absences;
using HoshiBot.Discord.AnonymousMessages;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.RoeViolations;
using HoshiBot.Discord.Scheduling;
using HoshiBot.Discord.Tickets;
using HoshiBot.Host;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddDiscordGateway(options => options.Intents = GatewayIntents.Guilds)
    .AddApplicationCommands()
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    .AddComponentInteractions<UserMenuInteraction, UserMenuInteractionContext>()
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
    .AddComponentInteractions<ModalInteraction, ModalInteractionContext>()
    .AddGatewayHandlers(typeof(Program).Assembly);

builder.Services.AddHoshiBotDatabase(builder.Configuration);
builder.Services.AddSingleton(new EmbedBrandingOptions(builder.Configuration["PublicWebBaseUrl"] ?? ""));
builder.Services.AddScoped<EmbedBranding>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<TerritoryCaptureDigestService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<RoeViolationService>();
builder.Services.AddScoped<AbsenceService>();
builder.Services.AddScoped<AnonymousMessageService>();
builder.Services.AddScoped<PendingModalInputService>();
builder.Services.AddScoped<GuildFeatureService>();

builder.Services.AddQuartz(quartz =>
{
    var heartbeatJobKey = new JobKey(nameof(HeartbeatJob));
    quartz.AddJob<HeartbeatJob>(heartbeatJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(heartbeatJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    var nicknameSyncJobKey = new JobKey(nameof(NicknameSyncJob));
    quartz.AddJob<NicknameSyncJob>(nicknameSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(nicknameSyncJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var notificationRoleSyncJobKey = new JobKey(nameof(NotificationRoleSyncJob));
    quartz.AddJob<NotificationRoleSyncJob>(notificationRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(notificationRoleSyncJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var threadCleanupJobKey = new JobKey(nameof(ThreadCleanupJob));
    quartz.AddJob<ThreadCleanupJob>(threadCleanupJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(threadCleanupJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var raidWarningJobKey = new JobKey(nameof(RaidWarningJob));
    quartz.AddJob<RaidWarningJob>(raidWarningJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(raidWarningJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(5).RepeatForever()));

    var shieldWarningJobKey = new JobKey(nameof(ShieldWarningJob));
    quartz.AddJob<ShieldWarningJob>(shieldWarningJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(shieldWarningJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(5).RepeatForever()));

    var territoryCaptureWeeklyDigestJobKey = new JobKey(nameof(TerritoryCaptureWeeklyDigestJob));
    quartz.AddJob<TerritoryCaptureWeeklyDigestJob>(territoryCaptureWeeklyDigestJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(territoryCaptureWeeklyDigestJobKey)
            .WithCronSchedule("0 0 9 ? * MON"));

    var territoryCaptureDailyDigestJobKey = new JobKey(nameof(TerritoryCaptureDailyDigestJob));
    quartz.AddJob<TerritoryCaptureDailyDigestJob>(territoryCaptureDailyDigestJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(territoryCaptureDailyDigestJobKey)
            .WithCronSchedule("0 0 19 * * ?"));

    var territoryCaptureRoleSyncJobKey = new JobKey(nameof(TerritoryCaptureRoleSyncJob));
    quartz.AddJob<TerritoryCaptureRoleSyncJob>(territoryCaptureRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(territoryCaptureRoleSyncJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var announcementCounterRefreshJobKey = new JobKey(nameof(AnnouncementCounterRefreshJob));
    quartz.AddJob<AnnouncementCounterRefreshJob>(announcementCounterRefreshJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(announcementCounterRefreshJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var absenceReportRefreshJobKey = new JobKey(nameof(AbsenceReportRefreshJob));
    quartz.AddJob<AbsenceReportRefreshJob>(absenceReportRefreshJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(absenceReportRefreshJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var pendingModalInputSweepJobKey = new JobKey(nameof(PendingModalInputSweepJob));
    quartz.AddJob<PendingModalInputSweepJob>(pendingModalInputSweepJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(pendingModalInputSweepJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var serverStatusNotifyJobKey = new JobKey(nameof(ServerStatusNotifyJob));
    quartz.AddJob<ServerStatusNotifyJob>(serverStatusNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(serverStatusNotifyJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(15).RepeatForever()));

    var incursionNotifyJobKey = new JobKey(nameof(IncursionNotifyJob));
    quartz.AddJob<IncursionNotifyJob>(incursionNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(incursionNotifyJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();

host.AddModules(typeof(PingModule).Assembly);

await host.Services.SeedHoshiBotDatabaseAsync(builder.Configuration);

await host.RunAsync();
