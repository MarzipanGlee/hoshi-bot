using System.Security.Claims;
using AspNet.Security.OAuth.Discord;
using HoshiBot.Data;
using HoshiBot.Data.Seeding;
using HoshiBot.Web.Authorization;
using HoshiBot.Web.Components;
using HoshiBot.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using NetCord;
using NetCord.Rest;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ClearProviders so the default console provider doesn't also write (Serilog's own
// console sink below replaces it) — otherwise every log line would be printed twice.
// The file sink also writes to a bind-mounted ./logs/web host directory (see
// compose.yaml) so logs survive without needing shell access to the container — see
// DEBUG.md.
builder.Logging.ClearProviders();
builder.Services.AddSerilog((services, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console()
    .WriteTo.File("logs/web-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddBootstrapBlazor();

builder.Services.AddHoshiBotDatabase(builder.Configuration);
builder.Services.AddQuickGridEntityFrameworkAdapter();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("DiscordUserApi", client =>
{
    client.BaseAddress = new Uri("https://discord.com/api/v10/");
});

builder.Services.AddHttpClient(nameof(StfcSystemSyncService));
builder.Services.AddHostedService<StfcSystemAutoSyncService>();
builder.Services.AddHostedService<TerritoryServiceAutoSyncService>();
builder.Services.AddHostedService<TerritoryOwnershipAutoSyncService>();

// territory.lol / api.stfc.pro can 403 a User-Agent-less client (bot protection), so a realistic
// browser UA is required — same gotcha as the Host's news/release clients.
const string browserUserAgent =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

var territoryLolBaseUrl = builder.Configuration["TerritoryLol:BaseUrl"] ?? "https://territory.lol/";
builder.Services.AddHttpClient(nameof(TerritoryServiceSyncService), client =>
{
    client.BaseAddress = new Uri(territoryLolBaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(browserUserAgent);
});

var stfcProBaseUrl = builder.Configuration["StfcPro:BaseUrl"] ?? "https://api.stfc.pro/";
builder.Services.AddHttpClient(nameof(StfcTerritoryOwnershipSyncService), client =>
{
    client.BaseAddress = new Uri(stfcProBaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(browserUserAgent);
});

builder.Services.AddSingleton(new RestClient(new BotToken(builder.Configuration["Discord:Token"]!)));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    // Default LoginPath/AccessDeniedPath are "/Account/Login" and "/Account/AccessDenied",
    // neither of which exist here — without this, a not-logged-in or logged-in-but-wrong-
    // role visitor hitting an [Authorize]'d /manage route on a fresh page load (as opposed
    // to an in-app Blazor navigation, which goes through AuthorizeRouteView's NotAuthorized
    // template instead) gets a 404 instead of landing on the home page.
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"]!;
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
        options.Scope.Add("guilds");
        options.SaveTokens = true;
        // Discord's /users/@me includes the client locale (identify scope) but the provider
        // doesn't map it by default — /me uses it to show what "Automatic" language resolves
        // to (and to seed DiscordUser.DiscordLocale before the bot ever saw an interaction).
        options.ClaimActions.MapJsonKey(DiscordAccountLink.LocaleClaim, "locale");
    })
    // A second Discord handler used *only* to prove ownership of another Discord account from
    // /me, never to sign in. Its ticket lands in the short-lived DiscordAccountLink.ExternalScheme
    // cookie (SignInScheme below) instead of the login cookie — without that, approving the prompt
    // would silently switch who's logged in rather than link the two accounts. Needs its own
    // CallbackPath (a scheme owns exactly one), so /signin-discord-link must also be registered as
    // an OAuth2 redirect URI on the Discord application.
    .AddCookie(DiscordAccountLink.ExternalScheme, options =>
    {
        options.Cookie.Name = ".HoshiBot.DiscordLink";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
    })
    .AddDiscord(DiscordAccountLink.Scheme, options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"]!;
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
        options.CallbackPath = DiscordAccountLink.CallbackPath;
        options.SignInScheme = DiscordAccountLink.ExternalScheme;
        // No "guilds" scope and no SaveTokens: all this flow needs is the other account's id.
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            // Without prompt=consent Discord silently re-approves the account already signed in
            // there, so the user never gets the chance to switch to the one they mean to link.
            context.Response.Redirect(context.RedirectUri + "&prompt=consent");
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            // Anything that reaches the callback without a usable Discord response: the user
            // declined the prompt, the state expired, or someone opened /signin-discord-link
            // directly (it's the handler's callback, not a page). The framework's default is to
            // rethrow, which renders the raw error page — send them back to /me instead.
            context.Response.Redirect($"/me?{DiscordAccountLink.ResultQueryKey}=failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("GlobalAdmin", policy => policy.Requirements.Add(new GlobalAdminRequirement()));
builder.Services.AddScoped<IAuthorizationHandler, GuildAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, GlobalAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SupportModeGuildAdminHandler>();
builder.Services.AddHoshiBotDataServices();
builder.Services.AddScoped<GuildAccessService>();
builder.Services.AddScoped<DiscordUserGuildsService>();
builder.Services.AddScoped<DiscordGuildDataService>();
builder.Services.AddScoped<PermissionAuditService>();
builder.Services.AddScoped<StfcPlayerImportService>();
builder.Services.AddScoped<StfcAllianceImportService>();
builder.Services.AddScoped<StfcCatalogImportService>();
builder.Services.AddScoped<StfcServerStatusImportService>();
builder.Services.AddScoped<StfcTerritoryOwnershipImportService>();
builder.Services.AddScoped<StfcTerritoryOwnershipSyncService>();
builder.Services.AddScoped<TerritoryServiceSyncService>();
builder.Services.AddScoped<StfcSystemSyncService>();
builder.Services.AddScoped<CurrentGuildContext>();
builder.Services.AddScoped<CurrentAllianceContext>();
builder.Services.AddScoped<SupportModeContext>();
builder.Services.AddScoped<WebRequestLanguage>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"] ?? "keys"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// MapRazorComponents only registers endpoints for discovered @page routes — there's no
// catch-all, so a URL matching none of them 404s at the routing layer itself, before the
// Blazor Router ever runs (meaning Routes.razor's own <NotFound> template never fires for
// a fresh browser navigation, only for in-app client-side navigation to a bad route within
// an already-connected circuit). This re-executes the pipeline against a real page instead,
// keeping the 404 status but rendering actual content.
app.UseStatusCodePagesWithReExecute("/not-found");

// Behind nginx, which terminates TLS and proxies over plain HTTP — without this, the app
// thinks every request is HTTP, breaking HTTPS redirection and building OAuth redirect_uri
// values as http:// instead of https://. KnownNetworks/KnownProxies are cleared because the
// proxy hop arrives via the Docker bridge network, not loopback.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (string? returnUrl) => Results.Challenge(
    new AuthenticationProperties { RedirectUri = returnUrl ?? "/manage" },
    [DiscordAuthenticationDefaults.AuthenticationScheme]));

app.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

// "Link another Discord account" from /me: send the user to Discord as themselves-but-elsewhere,
// and on the way back record that both accounts are the same person by giving them the union of
// their player links (PlayerLinkService.MergeAccountsAsync — a shared player IS the link).
app.MapGet(DiscordAccountLink.StartPath, () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = DiscordAccountLink.ReturnPath },
    [DiscordAccountLink.Scheme]))
    .RequireAuthorization();

app.MapGet(DiscordAccountLink.ReturnPath, async (HttpContext http, PlayerLinkService playerLinkService) =>
{
    var result = await http.AuthenticateAsync(DiscordAccountLink.ExternalScheme);
    // Consumed either way — this cookie exists only to carry one identity across the round-trip.
    await http.SignOutAsync(DiscordAccountLink.ExternalScheme);

    var outcome = "failed";
    if (result.Succeeded
        && ulong.TryParse(result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var otherUserId)
        && ulong.TryParse(http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
    {
        outcome = (await playerLinkService.MergeAccountsAsync(userId, otherUserId)) switch
        {
            AccountMergeOutcome.Linked => "ok",
            AccountMergeOutcome.AlreadyLinked => "already",
            AccountMergeOutcome.SameAccount => "self",
            AccountMergeOutcome.NoPlayers => "noplayers",
            _ => "failed",
        };
    }

    return Results.LocalRedirect($"/me?{DiscordAccountLink.ResultQueryKey}={outcome}");
})
    .RequireAuthorization();

// Public bot-installation link — redirects to Discord's own consent screen with the
// permissions the bot actually needs pre-filled, so an admin adding it to a new guild
// doesn't have to manually tick them (or under/over-grant). Keep this bitmask in sync
// with what the bot's commands/jobs actually call: ManageRoles (notification/TC role
// sync, and creating roles from the Setup Wizard), ManageChannels (creating channels/
// categories from the Setup Wizard), ManageNicknames (nickname sync), PinMessages
// (pinning the weekly TC digest — a bit Discord split out of Manage Messages; a bot with
// only Manage Messages still gets a 403 on the pin call), ManageThreads (closing/removing
// threads), EmbedLinks (digest/report embeds), ReadMessageHistory (reading announcement
// drafts back to publish them), AddReactions (decorating announcement drafts with the
// 🟩 🟨 🟥 🟦 severity reactions, which is how an announcement gets published),
// MentionEveryone (pinging the — usually non-mentionable — TC/notification roles on the
// daily/weekly digest), SendMessages/ViewChannel (posting at all).
//
// Deliberately NOT here: ManageMessages. Clearing the clicked severity reaction would need it
// (see AnnouncementDraftService), and a guild-wide "delete anyone's messages" grant is far too
// much to ask for that — an admin who wants it can grant it on the draft channel alone.
//
// Guilds that installed the bot before a permission was added here keep their old grant: they
// re-invite, or fix it per channel. The permission audit page is what surfaces that.
app.MapGet("/invite", (IConfiguration config, ulong? guildId) =>
{
    const Permissions botPermissions = Permissions.ViewChannel | Permissions.SendMessages | Permissions.EmbedLinks |
        Permissions.PinMessages | Permissions.ManageThreads | Permissions.ManageRoles | Permissions.ManageNicknames |
        Permissions.ManageChannels | Permissions.ReadMessageHistory | Permissions.AddReactions | Permissions.MentionEveryone;

    var clientId = config["Discord:ClientId"];
    var url = $"https://discord.com/oauth2/authorize?client_id={clientId}&permissions={(ulong)botPermissions}&scope=bot%20applications.commands";
    // guildId pre-selects the target server in Discord's own consent screen, mirroring the
    // "+ install" affordance on Discord's native server list — used by the guild-picker
    // page (Guilds/Index.razor) for guilds the user manages but the bot hasn't joined yet.
    if (guildId is not null)
        url += $"&guild_id={guildId}&disable_guild_select=true";
    return Results.Redirect(url);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.Services.SeedHoshiBotDatabaseAsync();
await app.Services.SeedGlobalAdminsIfEmptyAsync(builder.Configuration);

await app.RunAsync();
