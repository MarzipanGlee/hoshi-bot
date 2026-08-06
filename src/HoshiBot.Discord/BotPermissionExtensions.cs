using HoshiBot.Domain.Entities;

namespace HoshiBot.Discord;

// The NetCord edge for Domain's BotPermission — a plain cast, because BotPermission's values ARE
// Discord's bit positions by construction (see the enum, and the parity test in
// HoshiBot.Domain.Tests that is what actually holds that guarantee up).
//
// NetCord.Permissions is spelled out in full on purpose: this file sits in the HoshiBot.Discord
// namespace, which now also contains a child namespace called Permissions, and an unqualified
// `Permissions` binds to that namespace rather than the type ("is a namespace but is used like a
// type"). The same applies to any other file added at this project's root.
//
// HoshiBot.Web has its own copy: the two projects share no NetCord-referencing assembly, and a
// two-line cast in each beats inventing one just to hold it.
public static class BotPermissionExtensions
{
    public static NetCord.Permissions ToNetCord(this BotPermission permission) => (NetCord.Permissions)(ulong)permission;

    public static BotPermission ToDomain(this NetCord.Permissions permissions) => (BotPermission)(ulong)permissions;
}
