using HoshiBot.Data;
using HoshiBot.Discord;
using HoshiBot.Discord.Absences;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.AnonymousMessages;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.CommandBridge;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.RoeViolations;
using HoshiBot.Discord.Scheduling;
using HoshiBot.Discord.StfcNews;
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
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// ClearProviders so the default console provider doesn't also write (Serilog's own
// console sink below replaces it) — otherwise every log line would be printed twice.
// The file sink also writes to a bind-mounted ./logs/bot host directory (see
// compose.yaml) so logs survive without needing shell access to the container — see
// DEBUG.md.
builder.Logging.ClearProviders();
builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console()
    .WriteTo.File("logs/bot-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

builder.Services
    // GuildUsers (NetCord's name for Discord's "GUILD_MEMBERS" intent) is required for
    // StfcNewsNotifyJob's role-count proxy (RestClient.GetGuildUsersAsync) — also needs the
    // privileged Server Members Intent enabled for this bot application in the Discord
    // Developer Portal, or member fetches will fail/return incomplete data regardless of
    // this flag.
    // GuildMessages + MessageContent power the AI chat feature (AiChatMessageHandler reads
    // arbitrary member messages). MessageContent is a PRIVILEGED intent — it must also be
    // enabled for this bot application in the Discord Developer Portal, or message.Content
    // arrives empty and the AI never sees anything to answer.
    .AddDiscordGateway(options => options.Intents =
        GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent)
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
builder.Services.AddScoped<GuildFeatureSettingsService>();
builder.Services.AddScoped<GuildFeatureChannelService>();
builder.Services.AddScoped<GuildAllianceService>();
builder.Services.AddScoped<CommandBridgeHubService>();
builder.Services.AddScoped<BetaTesterService>();
builder.Services.AddScoped<StfcNewsService>();
builder.Services.AddScoped<IAiChatProvider, GeminiClient>();
builder.Services.AddScoped<IAiChatProvider, OllamaClient>();
builder.Services.AddScoped<AiChatIndexService>();
builder.Services.AddScoped<AiChatService>();

// The shared local Ollama server (compose service `ollama`); base URL is deployment config
// (Ollama:BaseUrl), not a per-guild secret. Long, configurable timeout — local model generation
// is slow, especially the first (cold-load) request or an 8B+ model on CPU-only hardware. Tune
// Ollama:TimeoutSeconds up for big models on CPU, or use a small model for usable latency.
builder.Services.AddHttpClient(nameof(OllamaClient), client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 120);
});

// A bare, User-Agent-less HttpClient gets a 403 from startrekfleetcommand.com's WordPress
// bot protection (confirmed against the real feed) — both of these hit external sites with
// similar bot-detection surfaces (a WordPress-hosted blog; Google/Apple's app stores), so a
// realistic browser User-Agent is applied to both, not just the one that's already failed.
const string BrowserUserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
builder.Services.AddHttpClient(nameof(StfcNewsNotifyJob), client =>
    client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent));
builder.Services.AddHttpClient(nameof(StfcClientReleaseNotifyJob), client =>
    client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent));

builder.Services.AddQuartz(quartz =>
{
    var heartbeatJobKey = new JobKey(nameof(HeartbeatJob));
    quartz.AddJob<HeartbeatJob>(heartbeatJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(heartbeatJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    // Rebuilds the AI-chat knowledge search index (history + forum threads + edit reconcile);
    // live indexing keeps new messages fresh between these hourly runs.
    var aiChatIndexJobKey = new JobKey(nameof(AiChatIndexJob));
    quartz.AddJob<AiChatIndexJob>(aiChatIndexJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(aiChatIndexJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(60).RepeatForever()));

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

    var commandBridgeRepublishJobKey = new JobKey(nameof(CommandBridgeRepublishJob));
    quartz.AddJob<CommandBridgeRepublishJob>(commandBridgeRepublishJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(commandBridgeRepublishJobKey)
            // Short interval: this backs the Web "Publish" button, which polls for completion.
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(5).RepeatForever()));

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

    var rankRoleSyncJobKey = new JobKey(nameof(RankRoleSyncJob));
    quartz.AddJob<RankRoleSyncJob>(rankRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(rankRoleSyncJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var opsLevelRoleSyncJobKey = new JobKey(nameof(OpsLevelRoleSyncJob));
    quartz.AddJob<OpsLevelRoleSyncJob>(opsLevelRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(opsLevelRoleSyncJobKey)
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

    var infiniteIncursionsNotifyJobKey = new JobKey(nameof(InfiniteIncursionsNotifyJob));
    quartz.AddJob<InfiniteIncursionsNotifyJob>(infiniteIncursionsNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(infiniteIncursionsNotifyJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    var allianceTournamentNotifyJobKey = new JobKey(nameof(AllianceTournamentNotifyJob));
    quartz.AddJob<AllianceTournamentNotifyJob>(allianceTournamentNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(allianceTournamentNotifyJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    var stfcNewsNotifyJobKey = new JobKey(nameof(StfcNewsNotifyJob));
    quartz.AddJob<StfcNewsNotifyJob>(stfcNewsNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(stfcNewsNotifyJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(30).RepeatForever()));

    var stfcNewsStatsRefreshJobKey = new JobKey(nameof(StfcNewsStatsRefreshJob));
    quartz.AddJob<StfcNewsStatsRefreshJob>(stfcNewsStatsRefreshJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(stfcNewsStatsRefreshJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(5).RepeatForever()));

    var stfcClientReleaseNotifyJobKey = new JobKey(nameof(StfcClientReleaseNotifyJob));
    quartz.AddJob<StfcClientReleaseNotifyJob>(stfcClientReleaseNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(stfcClientReleaseNotifyJobKey)
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(60).RepeatForever()));
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();

host.AddModules(typeof(PingModule).Assembly);

await host.Services.SeedHoshiBotDatabaseAsync();

await host.RunAsync();
