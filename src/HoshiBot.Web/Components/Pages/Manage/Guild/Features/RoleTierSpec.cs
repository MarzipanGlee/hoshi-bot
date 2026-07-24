namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features;

// One "tier" role a RoleTierEditor manages: its default role name (also the create-on-blank name)
// and the settings key its chosen role id is stored under. Note is optional per-tier display text
// (Ops-level roles show their ops-level range there; rank roles leave it null and show an icon).
public record RoleTierSpec(string DefaultName, string SettingKey, string? Note = null);
