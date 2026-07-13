using AspNet.Security.OAuth.Discord;
using HoshiBot.Data;
using HoshiBot.Web.Components;
using HoshiBot.Web.Authorization;
using HoshiBot.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
builder.Services.AddHostedService<StfcSystemSyncService>();

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
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("GlobalAdmin", policy => policy.Requirements.Add(new GlobalAdminRequirement()));
builder.Services.AddScoped<IAuthorizationHandler, GuildAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, GlobalAdminHandler>();
builder.Services.AddScoped<GuildFeatureService>();
builder.Services.AddScoped<GuildFeatureSettingsService>();
builder.Services.AddScoped<GuildAccessService>();
builder.Services.AddScoped<DiscordUserGuildsService>();
builder.Services.AddScoped<DiscordGuildDataService>();
builder.Services.AddScoped<StfcPlayerImportService>();
builder.Services.AddScoped<CurrentGuildContext>();

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

// Public bot-installation link — redirects to Discord's own consent screen with the
// permissions the bot actually needs pre-filled, so an admin adding it to a new guild
// doesn't have to manually tick them (or under/over-grant). Keep this bitmask in sync
// with what the bot's commands/jobs actually call: ManageRoles (notification/TC role
// sync, and creating roles from the Setup Wizard), ManageChannels (creating channels/
// categories from the Setup Wizard), ManageNicknames (nickname sync), ManageMessages
// (pinning the weekly TC digest), ManageThreads (closing/removing threads), EmbedLinks
// (digest/report embeds), SendMessages/ViewChannel (posting at all).
app.MapGet("/invite", (IConfiguration config, ulong? guildId) =>
{
    const Permissions botPermissions = Permissions.ViewChannel | Permissions.SendMessages | Permissions.EmbedLinks |
        Permissions.ManageMessages | Permissions.ManageThreads | Permissions.ManageRoles | Permissions.ManageNicknames |
        Permissions.ManageChannels;

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
