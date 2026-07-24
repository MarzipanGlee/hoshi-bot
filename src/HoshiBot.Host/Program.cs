using HoshiBot.Data;
using HoshiBot.Discord;
using HoshiBot.Discord.Absences;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.AnnouncementForwarder;
using HoshiBot.Discord.AnonymousMessages;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.CommandBridge;
using HoshiBot.Discord.MemberLore;
using HoshiBot.Discord.MemberOnboarding;
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
        GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        // DirectMessages: receive MESSAGE_CREATE for DMs (member-lore interview replies). Not a
        // privileged intent (no portal toggle); DM content always arrives without MessageContent.
        | GatewayIntents.DirectMessages)
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
builder.Services.AddScoped<MemberNoteService>();
builder.Services.AddScoped<GuildFeatureChannelService>();
builder.Services.AddScoped<GuildAllianceService>();
builder.Services.AddScoped<AiChatHealthService>();
builder.Services.AddScoped<CommandBridgeHubService>();
builder.Services.AddScoped<BetaTesterService>();
builder.Services.AddScoped<StfcNewsService>();
builder.Services.AddScoped<IAiChatProvider, GeminiClient>();
builder.Services.AddScoped<IAiChatProvider, OllamaClient>();
builder.Services.AddScoped<IAiEmbeddingProvider, OllamaEmbeddingProvider>();
builder.Services.AddScoped<IAiEmbeddingProvider, GeminiEmbeddingProvider>();
builder.Services.AddScoped<AiChatEmbeddingService>();
builder.Services.AddScoped<AiChatIndexService>();
builder.Services.AddScoped<AiChatService>();
builder.Services.AddScoped<AiChatModelResolver>();
builder.Services.AddScoped<MemberInterviewService>();
builder.Services.AddScoped<MemberNoteExtractor>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<MemoryExtractor>();
builder.Services.AddScoped<AnnouncementTranslator>();
builder.Services.AddScoped<AnnouncementForwarderService>();
builder.Services.AddScoped<PlayerLinkService>();
builder.Services.AddScoped<MemberOnboardingService>();

// The shared local Ollama server (compose service `ollama`); base URL is deployment config
// (Ollama:BaseUrl), not a per-guild secret. Long, configurable timeout — local model generation
// is slow, especially the first (cold-load) request or an 8B+ model on CPU-only hardware. Tune
// Ollama:TimeoutSeconds up for big models on CPU, or use a small model for usable latency.
builder.Services.AddHttpClient(nameof(OllamaClient), client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 120);
});

// Preloads the in-use Ollama models at startup so the first reply/search isn't cold (see the service).
builder.Services.AddHostedService<OllamaWarmupService>();

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

// Territory Capture digests are scheduled in the alliance's local wall-clock time (Swiss guild),
// NOT the container's TZ (production/test containers run in UTC). Pinning the zone here means
// "19:00" stays 19:00 CEST/CET across servers and survives the summer/winter DST shift, matching
// what the legacy bot posted (daily 17:00 UTC = 19:00 CEST). Without it the cron fires at 19:00
// UTC — two hours late in summer.
var digestTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

builder.Services.AddQuartz(quartz =>
{
    // Persist jobs/triggers in Postgres so a fire missed while the host is down (deploys, crashes)
    // is replayed once on the next startup instead of lost. This matters most for the low-frequency
    // Territory Capture digests (daily 19:00, Monday 09:00): with the default in-memory RAMJobStore
    // they only ever fired if the process happened to be alive at that exact second, so frequent
    // redeploys could skip them for days. IgnoreDuplicates (set on QuartzOptions below) is what makes
    // the replay actually work — without it, startup reschedules every trigger and resets its state.
    quartz.UsePersistentStore(store =>
    {
        store.UseProperties = true;
        store.UsePostgres(pg =>
        {
            pg.ConnectionString = builder.Configuration.GetConnectionString("HoshiBotDbContext")
                ?? throw new InvalidOperationException(
                    "Connection string 'HoshiBotDbContext' is required for the Quartz persistent store.");
            pg.TablePrefix = "QRTZ_";
        });
        store.UseSystemTextJsonSerializer();
        // Clustering intentionally left off — single scheduler instance.
    });

    // Every trigger below carries an explicit, stable .WithIdentity("<job>-trigger"). This is
    // required with the persistent store: an un-named trigger gets a fresh random GUID name each
    // startup, so IgnoreDuplicates can't recognise it and every restart would pile up another
    // duplicate trigger per job (each firing the job again) instead of reusing the persisted one.
    var heartbeatJobKey = new JobKey(nameof(HeartbeatJob));
    quartz.AddJob<HeartbeatJob>(heartbeatJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(heartbeatJobKey)
            .WithIdentity($"{heartbeatJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    // Rebuilds the AI-chat knowledge search index: progressive history backfill (a bounded step
    // backward per run), edit catch-up, and the embedding pass (also per-run capped). Live indexing
    // keeps brand-new messages fresh between runs. 20 min so history + embeddings catch up quickly;
    // cheap at steady state (recent page only, completed channels skip history, embed pass no-ops).
    var aiChatIndexJobKey = new JobKey(nameof(AiChatIndexJob));
    quartz.AddJob<AiChatIndexJob>(aiChatIndexJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(aiChatIndexJobKey)
            .WithIdentity($"{aiChatIndexJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(20).RepeatForever()));

    var nicknameSyncJobKey = new JobKey(nameof(NicknameSyncJob));
    quartz.AddJob<NicknameSyncJob>(nicknameSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(nicknameSyncJobKey)
            .WithIdentity($"{nicknameSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var notificationRoleSyncJobKey = new JobKey(nameof(NotificationRoleSyncJob));
    quartz.AddJob<NotificationRoleSyncJob>(notificationRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(notificationRoleSyncJobKey)
            .WithIdentity($"{notificationRoleSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var threadCleanupJobKey = new JobKey(nameof(ThreadCleanupJob));
    quartz.AddJob<ThreadCleanupJob>(threadCleanupJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(threadCleanupJobKey)
            .WithIdentity($"{threadCleanupJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var commandBridgeRepublishJobKey = new JobKey(nameof(CommandBridgeRepublishJob));
    quartz.AddJob<CommandBridgeRepublishJob>(commandBridgeRepublishJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(commandBridgeRepublishJobKey)
            .WithIdentity($"{commandBridgeRepublishJobKey.Name}-trigger")
            // Short interval: this backs the Web "Publish" button, which polls for completion.
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(5).RepeatForever()));

    var raidWarningJobKey = new JobKey(nameof(RaidWarningJob));
    quartz.AddJob<RaidWarningJob>(raidWarningJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(raidWarningJobKey)
            .WithIdentity($"{raidWarningJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(5).RepeatForever()));

    var shieldWarningJobKey = new JobKey(nameof(ShieldWarningJob));
    quartz.AddJob<ShieldWarningJob>(shieldWarningJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(shieldWarningJobKey)
            .WithIdentity($"{shieldWarningJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(5).RepeatForever()));

    var territoryCaptureWeeklyDigestJobKey = new JobKey(nameof(TerritoryCaptureWeeklyDigestJob));
    quartz.AddJob<TerritoryCaptureWeeklyDigestJob>(territoryCaptureWeeklyDigestJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(territoryCaptureWeeklyDigestJobKey)
            .WithIdentity($"{territoryCaptureWeeklyDigestJobKey.Name}-trigger")
            // FireAndProceed: if the weekly fire was missed (host down), run it once on startup,
            // then resume the normal schedule — don't skip the week or replay every missed week.
            .WithCronSchedule("0 0 9 ? * MON", x => x
                .InTimeZone(digestTimeZone)
                .WithMisfireHandlingInstructionFireAndProceed()));

    var territoryCaptureDailyDigestJobKey = new JobKey(nameof(TerritoryCaptureDailyDigestJob));
    quartz.AddJob<TerritoryCaptureDailyDigestJob>(territoryCaptureDailyDigestJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(territoryCaptureDailyDigestJobKey)
            .WithIdentity($"{territoryCaptureDailyDigestJobKey.Name}-trigger")
            // FireAndProceed: replay a missed daily fire once on startup, then carry on.
            .WithCronSchedule("0 0 19 * * ?", x => x
                .InTimeZone(digestTimeZone)
                .WithMisfireHandlingInstructionFireAndProceed()));

    var territoryCaptureRoleSyncJobKey = new JobKey(nameof(TerritoryCaptureRoleSyncJob));
    quartz.AddJob<TerritoryCaptureRoleSyncJob>(territoryCaptureRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(territoryCaptureRoleSyncJobKey)
            .WithIdentity($"{territoryCaptureRoleSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var memberInterviewInviteJobKey = new JobKey(nameof(MemberInterviewInviteJob));
    quartz.AddJob<MemberInterviewInviteJob>(memberInterviewInviteJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(memberInterviewInviteJobKey)
            .WithIdentity($"{memberInterviewInviteJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(20).RepeatForever()));

    var memberInterviewExtractionJobKey = new JobKey(nameof(MemberInterviewExtractionJob));
    quartz.AddJob<MemberInterviewExtractionJob>(memberInterviewExtractionJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(memberInterviewExtractionJobKey)
            .WithIdentity($"{memberInterviewExtractionJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var memoryConsolidationJobKey = new JobKey(nameof(MemoryConsolidationJob));
    quartz.AddJob<MemoryConsolidationJob>(memoryConsolidationJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(memoryConsolidationJobKey)
            .WithIdentity($"{memoryConsolidationJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var announcementForwarderCatchUpJobKey = new JobKey(nameof(AnnouncementForwarderCatchUpJob));
    quartz.AddJob<AnnouncementForwarderCatchUpJob>(announcementForwarderCatchUpJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(announcementForwarderCatchUpJobKey)
            .WithIdentity($"{announcementForwarderCatchUpJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var playerLinkSyncJobKey = new JobKey(nameof(PlayerLinkSyncJob));
    quartz.AddJob<PlayerLinkSyncJob>(playerLinkSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(playerLinkSyncJobKey)
            .WithIdentity($"{playerLinkSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var memberOnboardingSyncJobKey = new JobKey(nameof(MemberOnboardingSyncJob));
    quartz.AddJob<MemberOnboardingSyncJob>(memberOnboardingSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(memberOnboardingSyncJobKey)
            .WithIdentity($"{memberOnboardingSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(20).RepeatForever()));

    var rankRoleSyncJobKey = new JobKey(nameof(RankRoleSyncJob));
    quartz.AddJob<RankRoleSyncJob>(rankRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(rankRoleSyncJobKey)
            .WithIdentity($"{rankRoleSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var opsLevelRoleSyncJobKey = new JobKey(nameof(OpsLevelRoleSyncJob));
    quartz.AddJob<OpsLevelRoleSyncJob>(opsLevelRoleSyncJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(opsLevelRoleSyncJobKey)
            .WithIdentity($"{opsLevelRoleSyncJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever()));

    var announcementCounterRefreshJobKey = new JobKey(nameof(AnnouncementCounterRefreshJob));
    quartz.AddJob<AnnouncementCounterRefreshJob>(announcementCounterRefreshJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(announcementCounterRefreshJobKey)
            .WithIdentity($"{announcementCounterRefreshJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var absenceReportRefreshJobKey = new JobKey(nameof(AbsenceReportRefreshJob));
    quartz.AddJob<AbsenceReportRefreshJob>(absenceReportRefreshJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(absenceReportRefreshJobKey)
            .WithIdentity($"{absenceReportRefreshJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var pendingModalInputSweepJobKey = new JobKey(nameof(PendingModalInputSweepJob));
    quartz.AddJob<PendingModalInputSweepJob>(pendingModalInputSweepJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(pendingModalInputSweepJobKey)
            .WithIdentity($"{pendingModalInputSweepJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));

    var serverStatusNotifyJobKey = new JobKey(nameof(ServerStatusNotifyJob));
    quartz.AddJob<ServerStatusNotifyJob>(serverStatusNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(serverStatusNotifyJobKey)
            .WithIdentity($"{serverStatusNotifyJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInSeconds(15).RepeatForever()));

    var infiniteIncursionsNotifyJobKey = new JobKey(nameof(InfiniteIncursionsNotifyJob));
    quartz.AddJob<InfiniteIncursionsNotifyJob>(infiniteIncursionsNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(infiniteIncursionsNotifyJobKey)
            .WithIdentity($"{infiniteIncursionsNotifyJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    var allianceTournamentNotifyJobKey = new JobKey(nameof(AllianceTournamentNotifyJob));
    quartz.AddJob<AllianceTournamentNotifyJob>(allianceTournamentNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(allianceTournamentNotifyJobKey)
            .WithIdentity($"{allianceTournamentNotifyJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(1).RepeatForever()));

    var stfcNewsNotifyJobKey = new JobKey(nameof(StfcNewsNotifyJob));
    quartz.AddJob<StfcNewsNotifyJob>(stfcNewsNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(stfcNewsNotifyJobKey)
            .WithIdentity($"{stfcNewsNotifyJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(30).RepeatForever()));

    var stfcNewsStatsRefreshJobKey = new JobKey(nameof(StfcNewsStatsRefreshJob));
    quartz.AddJob<StfcNewsStatsRefreshJob>(stfcNewsStatsRefreshJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(stfcNewsStatsRefreshJobKey)
            .WithIdentity($"{stfcNewsStatsRefreshJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(5).RepeatForever()));

    var stfcClientReleaseNotifyJobKey = new JobKey(nameof(StfcClientReleaseNotifyJob));
    quartz.AddJob<StfcClientReleaseNotifyJob>(stfcClientReleaseNotifyJobKey)
        .AddTrigger(trigger => trigger
            .ForJob(stfcClientReleaseNotifyJobKey)
            .WithIdentity($"{stfcClientReleaseNotifyJobKey.Name}-trigger")
            .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(15).RepeatForever()));
});
// Seed each job/trigger into the persistent store only once. On every later restart the stored
// definitions stay authoritative, so a trigger's persisted next-fire-time — and therefore any
// missed-fire (misfire) state — survives the restart and gets replayed. Without IgnoreDuplicates,
// the DI startup RESCHEDULES every code-defined trigger, resetting its fire time to the future and
// silently defeating the misfire replay above (builds fine, tests pass, jobs just never catch up).
// Trade-off: changing a job's schedule in code no longer takes effect on its own — to roll out a
// schedule change, delete that job's QRTZ_TRIGGERS row (or briefly flip these flags) and redeploy.
builder.Services.Configure<QuartzOptions>(options =>
{
    options.Scheduling.IgnoreDuplicates = true;
    options.Scheduling.OverWriteExistingData = false;
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();

host.AddModules(typeof(PingModule).Assembly);

await host.Services.SeedHoshiBotDatabaseAsync();

await host.RunAsync();
