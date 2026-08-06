using HoshiBot.Data;
using HoshiBot.Data.Seeding;
using HoshiBot.Discord;
using HoshiBot.Discord.Absences;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.Alerts;
using HoshiBot.Discord.AnnouncementForwarder;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.AnonymousMessages;
using HoshiBot.Discord.CommandBridge;
using HoshiBot.Discord.MemberLore;
using HoshiBot.Discord.MemberOnboarding;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.Permissions;
using HoshiBot.Discord.RoeViolations;
using HoshiBot.Discord.Scheduling;
using HoshiBot.Discord.StfcNews;
using HoshiBot.Discord.TerritoryCapture;
using HoshiBot.Discord.Tickets;
using HoshiBot.Domain.Entities;
using HoshiBot.Host;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;
using Quartz;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var commandLocalizations = new JsonLocalizationsProvider(new()
{
    DirectoryPath = Path.Combine(AppContext.BaseDirectory, "Localizations"),
});

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
    .AddDiscordGateway((options, services) =>
    {
        options.Intents =
            GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
            // DirectMessages: receive MESSAGE_CREATE for DMs (member-lore interview replies). Not a
            // privileged intent (no portal toggle); DM content always arrives without MessageContent.
            | GatewayIntents.DirectMessages
            // GuildMessageReactions: receive MESSAGE_REACTION_ADD, which is how an announcement draft
            // is published (AnnouncementDraftReactionHandler). Also not privileged.
            | GatewayIntents.GuildMessageReactions;

        // Every REST response the bot gets passes through this one handler, so the 401 kill switch
        // and the invalid-request meter need no changes at any call site. NetCord owns 429 entirely
        // (pre-emptive buckets, Retry-After aware) but tracks nothing about Discord's
        // 10,000-invalid-responses-per-10-minutes ban threshold, and does nothing at all on 401.
        options.RestClientConfiguration = new RestClientConfiguration
        {
            RequestHandler = new InvalidRequestTrackingHandler(
                new RestRequestHandler(),
                services.GetRequiredService<DiscordApiHealth>(),
                services.GetRequiredService<ILogger<InvalidRequestTrackingHandler>>()),
        };
    })
    // One application-command service: /hoshi-say is the only application command left (everything
    // else moved to the Command Bridge, the Web admin, or — for announcements — the draft-channel
    // reactions), so the startup log should say "1 command(s) registered". There used to be a second
    // AddApplicationCommands<MessageCommandInteraction, MessageCommandContext> for the "Create
    // preview" context-menu command, whose MessageCommandContext is a sibling of
    // ApplicationCommandContext rather than a subtype and so was invisible to this service. Should a
    // context-menu command ever return, it needs its own service back.
    //
    // LocalizationsProvider (shared instance, so the JSON files are only loaded once): localizes
    // command metadata (descriptions, option names/descriptions, enum choices — names stay English)
    // from Localizations/{locale}.json, anchored to the app directory so it works regardless of the
    // process working directory. The files load lazily on the first localization lookup during
    // startup command registration: a malformed file or a missing directory throws there and fails
    // startup, but a mistyped command/parameter key is silently ignored (the provider only looks up
    // keys the registered commands actually have).
    .AddApplicationCommands<SlashCommandInteraction, ApplicationCommandContext, AutocompleteInteractionContext>(
        options => options.LocalizationsProvider = commandLocalizations)
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    .AddComponentInteractions<UserMenuInteraction, UserMenuInteractionContext>()
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
    .AddComponentInteractions<ModalInteraction, ModalInteractionContext>()
    .AddGatewayHandlers(typeof(Program).Assembly);

builder.Services.AddHoshiBotDatabase(builder.Configuration);
builder.Services.AddHoshiBotDataServices();
builder.Services.AddSingleton(new EmbedBrandingOptions(builder.Configuration["PublicWebBaseUrl"] ?? ""));
builder.Services.AddScoped<EmbedBranding>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<TerritoryCaptureDigestService>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<AnnouncementDraftService>();
// Singletons: both outlive the per-fire Quartz scopes they serve. DiscordApiHealth holds the 401
// kill switch and the rolling invalid-request count; PermissionGuard caches each guild's role
// standing for 60s so a sync job resolves it once per run instead of once per member.
builder.Services.AddSingleton<DiscordApiHealth>();
builder.Services.AddSingleton<PermissionGuard>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<RoeViolationService>();
builder.Services.AddScoped<AbsenceService>();
builder.Services.AddScoped<AnonymousMessageService>();
builder.Services.AddScoped<PendingModalInputService>();
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
builder.Services.AddScoped<InterviewOpener>();
builder.Services.AddScoped<MemberNoteExtractor>();
builder.Services.AddScoped<MemoryExtractor>();
builder.Services.AddScoped<AnnouncementTranslator>();
builder.Services.AddScoped<AnnouncementForwarderService>();
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
    // Vetoes every job while the Discord token is invalid — one place, instead of each job
    // discovering it mid-run and burning invalid requests on the way (see DiscordTokenJobListener).
    quartz.AddTriggerListener<DiscordTokenTriggerListener>();

    // Persist jobs/triggers in Postgres so a fire missed while the host is down (deploys, crashes)
    // is replayed once on the next startup instead of lost. This matters most for the low-frequency
    // Territory Capture digests (daily 19:00, weekly 09:00): with the default in-memory RAMJobStore
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

    // Every trigger carries an explicit, stable .WithIdentity("<job>-trigger"): with the persistent
    // store an un-named trigger gets a fresh random GUID name each startup, so IgnoreDuplicates can't
    // recognise it and every restart would pile up another duplicate trigger per job. These helpers
    // derive the JobKey and that identity from typeof(TJob).Name so it can't be mistyped.
    void AddSimpleJob<TJob>(TimeSpan interval) where TJob : IJob
    {
        var key = new JobKey(typeof(TJob).Name);
        quartz.AddJob<TJob>(key).AddTrigger(t => t
            .ForJob(key)
            .WithIdentity($"{key.Name}-trigger")
            .WithSimpleSchedule(s => s.WithInterval(interval).RepeatForever()));
    }

    // FireAndProceed: replay a single missed fire once on startup (host was down), then resume the
    // normal schedule — don't skip the period or replay every missed one. Runs in digestTimeZone.
    void AddCronJob<TJob>(string cron) where TJob : IJob
    {
        var key = new JobKey(typeof(TJob).Name);
        quartz.AddJob<TJob>(key).AddTrigger(t => t
            .ForJob(key)
            .WithIdentity($"{key.Name}-trigger")
            .WithCronSchedule(cron, x => x.InTimeZone(digestTimeZone).WithMisfireHandlingInstructionFireAndProceed()));
    }
    AddSimpleJob<HeartbeatJob>(TimeSpan.FromMinutes(1));

    // Rebuilds the AI-chat knowledge search index: progressive history backfill (a bounded step
    // backward per run), edit catch-up, and the embedding pass (also per-run capped). Live indexing
    // keeps brand-new messages fresh between runs. 20 min so history + embeddings catch up quickly;
    // cheap at steady state (recent page only, completed channels skip history, embed pass no-ops).
    AddSimpleJob<AiChatIndexJob>(TimeSpan.FromMinutes(20));

    AddSimpleJob<NicknameSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<NotificationRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<ThreadCleanupJob>(TimeSpan.FromMinutes(15));

    // Short interval: this backs the Web "Publish" button, which polls for completion.
    AddSimpleJob<CommandBridgeRepublishJob>(TimeSpan.FromSeconds(5));

    AddSimpleJob<RaidWarningJob>(TimeSpan.FromMinutes(5));

    AddSimpleJob<ShieldWarningJob>(TimeSpan.FromMinutes(5));

    // Half-hourly sweep posting each alliance's weekly/daily digest at its own configured local time
    // (GuildAlliance.TimeZoneId, DST-aware) — replaces the two fixed Europe/Zurich crons. The whole-hour
    // Zurich trigger pin lands on the same instants as UTC :00/:30; the job body converts per-alliance.
    // The obsolete crons' persisted triggers are removed on startup (see the cleanup after Build()).
    AddCronJob<TerritoryCaptureDigestSweepJob>("0 0,30 * * * ?");

    AddSimpleJob<TerritoryCaptureRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<TerritoryCaptureReminderJob>(TimeSpan.FromMinutes(5));

    AddSimpleJob<MemberInterviewInviteJob>(TimeSpan.FromMinutes(20));

    AddSimpleJob<MemberInterviewExtractionJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<MemoryConsolidationJob>(TimeSpan.FromMinutes(15));

    AddSimpleJob<AnnouncementForwarderCatchUpJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<PlayerLinkSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<MemberOnboardingSyncJob>(TimeSpan.FromMinutes(20));

    AddSimpleJob<RankRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<OpsLevelRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<AllianceTagRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<ServerTagRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<ConditionalRoleSyncJob>(TimeSpan.FromMinutes(10));

    AddSimpleJob<AnnouncementCounterRefreshJob>(TimeSpan.FromMinutes(15));

    AddSimpleJob<AbsenceReportRefreshJob>(TimeSpan.FromMinutes(15));

    AddSimpleJob<PendingModalInputSweepJob>(TimeSpan.FromMinutes(15));

    AddSimpleJob<ServerStatusNotifyJob>(TimeSpan.FromSeconds(15));

    AddSimpleJob<InfiniteIncursionsNotifyJob>(TimeSpan.FromMinutes(1));

    AddSimpleJob<AllianceTournamentNotifyJob>(TimeSpan.FromMinutes(1));

    AddSimpleJob<StfcNewsNotifyJob>(TimeSpan.FromMinutes(30));

    AddSimpleJob<StfcNewsStatsRefreshJob>(TimeSpan.FromMinutes(5));

    AddSimpleJob<StfcClientReleaseNotifyJob>(TimeSpan.FromMinutes(15));
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
builder.Services.AddSingleton<DiscordTokenTriggerListener>();

var host = builder.Build();

// One-time removal of the two obsolete fixed-time digest cron jobs, now replaced by
// TerritoryCaptureDigestSweepJob. The persistent store keeps existing jobs (IgnoreDuplicates), so
// without this they would keep firing alongside the sweep. Runs before host.RunAsync() — i.e. before the
// Quartz hosted service Starts and recovers triggers — and is idempotent (DeleteJob no-ops if gone).
// Job keys are the literal type names AddJob derived from typeof(T).Name (the retained but obsolete
// TerritoryCaptureWeeklyDigestJob/TerritoryCaptureDailyDigestJob classes) — referenced as strings so
// the cleanup doesn't depend on those soon-to-be-deleted types.
var digestScheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
foreach (var obsoleteJob in new[] { "TerritoryCaptureWeeklyDigestJob", "TerritoryCaptureDailyDigestJob" })
    await digestScheduler.DeleteJob(new JobKey(obsoleteJob));

// Anchored to CommanderName — any type in HoshiBot.Discord would do, and this one is deliberately
// *not* a command/interaction module: the previous anchor (PingModule) took module discovery for the
// whole assembly down with it when its command was retired.
host.AddModules(typeof(CommanderName).Assembly);

await host.Services.SeedHoshiBotDatabaseAsync();

await host.RunAsync();
