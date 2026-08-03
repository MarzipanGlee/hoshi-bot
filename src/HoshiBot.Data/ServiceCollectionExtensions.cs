using HoshiBot.Data.Seeding;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HoshiBot.Data;

public static class ServiceCollectionExtensions
{
    // Registers both IDbContextFactory<HoshiBotDbContext> — a fresh, short-lived context per
    // operation, which every HoshiBot.Web page/service/authorization-handler must use, since a
    // Blazor Server circuit's DI scope spans the whole session (many page navigations), not one
    // request; sharing a single scoped DbContext across that span causes "a second operation was
    // started on this context instance" crashes the moment two components/pages touch it at
    // once — and a scoped HoshiBotDbContext derived from that factory, safe for HoshiBot.Discord's
    // command modules specifically because Discord.NET creates a fresh DI scope per interaction
    // (one command execution = one scope, not one bot-lifetime session). Registering both via
    // AddDbContext + AddDbContextFactory independently causes a DI lifetime-validation error (a
    // singleton factory can't consume the scoped DbContextOptions<T> that AddDbContext registers)
    // — deriving the scoped context from the factory instead avoids a second, conflicting
    // DbContextOptions<T> registration.
    public static IServiceCollection AddHoshiBotDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HoshiBotDbContext");

        services.AddDbContextFactory<HoshiBotDbContext>(options => options.UseNpgsql(connectionString, o => o.UseVector()));

        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<HoshiBotDbContext>>().CreateDbContext());

        // Encrypts secret-typed feature settings at rest (see GuildFeatureSettingsService.GetSecret/
        // SetSecret). Registered here so both host processes (Host + Web) get it wherever they use
        // the settings service; reads Secrets:EncryptionKey from their config.
        services.AddSingleton<SettingSecretProtector>();

        return services;
    }

    // The application services this assembly provides that both host processes (Host + Web)
    // consume. Registered in one place so the two Program.cs files can't drift apart — Web
    // once silently omitted MemberNoteService this way. Data services used by only one host
    // (e.g. the STFC import/sync services, Web-only) stay registered in that host's Program.cs.
    public static IServiceCollection AddHoshiBotDataServices(this IServiceCollection services)
    {
        services.AddScoped<GuildFeatureService>();
        services.AddScoped<GuildFeatureSettingsService>();
        services.AddScoped<GuildFeatureChannelService>();
        services.AddScoped<GuildAllianceService>();
        services.AddScoped<AiChatHealthService>();
        services.AddScoped<MemoryService>();
        services.AddScoped<PlayerLinkService>();
        services.AddScoped<ConditionalRoleService>();
        services.AddScoped<MemberNoteService>();
        services.AddScoped<LanguageResolver>();
        // Singleton: the resolved-language cache spans scopes by design (5-min TTL).
        services.AddSingleton<LanguageCache>();

        return services;
    }
}
