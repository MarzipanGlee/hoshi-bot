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
using NetCord;
using NetCord.Rest;

var builder = WebApplication.CreateBuilder(args);

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
    .AddCookie()
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"]!;
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
        options.Scope.Add("guilds");
        options.SaveTokens = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthorizationHandler, GuildAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, GlobalAdminHandler>();
builder.Services.AddScoped<GuildFeatureService>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"] ?? "keys"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (string? returnUrl) => Results.Challenge(
    new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
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
app.MapGet("/invite", (IConfiguration config) =>
{
    const Permissions botPermissions = Permissions.ViewChannel | Permissions.SendMessages | Permissions.EmbedLinks |
        Permissions.ManageMessages | Permissions.ManageThreads | Permissions.ManageRoles | Permissions.ManageNicknames |
        Permissions.ManageChannels;

    var clientId = config["Discord:ClientId"];
    var url = $"https://discord.com/oauth2/authorize?client_id={clientId}&permissions={(ulong)botPermissions}&scope=bot%20applications.commands";
    return Results.Redirect(url);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.Services.EnsureHoshiBotDatabaseCreatedIfSqliteAsync(builder.Configuration);
await app.Services.SeedGlobalAdminsIfEmptyAsync(builder.Configuration);
await app.Services.SeedStfcCatalogIfEmptyAsync();
await app.Services.SeedStfcTerritoriesIfEmptyAsync();
await app.Services.SeedGuildSettingsIfEmptyAsync();

await app.RunAsync();
