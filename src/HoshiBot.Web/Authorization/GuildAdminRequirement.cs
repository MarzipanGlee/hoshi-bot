using Microsoft.AspNetCore.Authorization;

namespace HoshiBot.Web.Authorization;

// Resource-based requirement: the resource passed to AuthorizeAsync is the target guild's ID (ulong),
// since whether a user qualifies depends on which guild is being managed.
public class GuildAdminRequirement : IAuthorizationRequirement;
