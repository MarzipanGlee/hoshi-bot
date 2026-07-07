using Microsoft.AspNetCore.Authorization;

namespace HoshiBot.Web.Authorization;

// Bot-wide admin check — no resource, unlike GuildAdminRequirement (which is scoped
// to a specific guild).
public class GlobalAdminRequirement : IAuthorizationRequirement;
