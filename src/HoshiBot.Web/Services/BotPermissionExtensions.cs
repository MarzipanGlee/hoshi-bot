using HoshiBot.Domain.Entities;
using NetCord;

namespace HoshiBot.Web.Services;

// The NetCord edge for Domain's BotPermission. It is a plain cast because BotPermission's values
// ARE Discord's bit positions by construction — see the comment on the enum, and
// HoshiBot.Domain.Tests.BotPermissionTests, which is what actually holds that guarantee up.
//
// ToDomain may carry bits that have no BotPermission name (Connect, Speak, …). That's harmless:
// every calculation here is `required & ~effective`, and display only ever enumerates the required
// set, never the effective one.
public static class BotPermissionExtensions
{
    public static Permissions ToNetCord(this BotPermission permission) => (Permissions)(ulong)permission;

    public static BotPermission ToDomain(this Permissions permissions) => (BotPermission)(ulong)permissions;
}
