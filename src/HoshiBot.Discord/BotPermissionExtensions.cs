using HoshiBot.Domain.Entities;
using NetCord;

namespace HoshiBot.Discord;

// The NetCord edge for Domain's BotPermission — a plain cast, because BotPermission's values ARE
// Discord's bit positions by construction (see the enum, and the parity test in
// HoshiBot.Domain.Tests that is what actually holds that guarantee up).
//
// HoshiBot.Web has its own copy: the two projects share no NetCord-referencing assembly, and a
// two-line cast in each beats inventing one just to hold it.
public static class BotPermissionExtensions
{
    public static Permissions ToNetCord(this BotPermission permission) => (Permissions)(ulong)permission;

    public static BotPermission ToDomain(this Permissions permissions) => (BotPermission)(ulong)permissions;
}
