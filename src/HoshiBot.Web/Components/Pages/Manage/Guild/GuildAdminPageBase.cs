using HoshiBot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace HoshiBot.Web.Components.Pages.Manage.Guild;

// Shared resource-scoped auth check for every /manage/guild/{GuildId}/** page. Mirrors
// FeatureEditorBase's shape: base owns the boilerplate, subclasses override a hook that
// only ever runs once authorization for ParsedGuildId has succeeded.
//
// Deliberately does its own manual AuthorizeAsync + NavigateTo("/") rather than
// [Authorize(Policy=...)]/AuthorizeRouteView: GuildAdminHandler needs the {GuildId} route
// value as its resource, and AuthorizeRouteView's pipeline always calls
// AuthorizeAsync(user, resource: null, ...) with no hook to supply a per-route resource.
public abstract class GuildAdminPageBase : ComponentBase
{
    [Parameter]
    public string GuildId { get; set; } = "";

    protected ulong ParsedGuildId => ulong.Parse(GuildId);

    [Inject]
    protected IAuthorizationService AuthorizationService { get; set; } = null!;

    [Inject]
    protected AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    [Inject]
    protected NavigationManager Nav { get; set; } = null!;

    // Replaces the old three-way (null/false/true) authorized field — there's no "denied"
    // state to render any more, only "still checking" vs "authorized".
    protected bool Authorized { get; private set; }

    protected override async Task OnParametersSetAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var result = await AuthorizationService.AuthorizeAsync(
            authState.User, ParsedGuildId, new GuildAdminRequirement());

        if (!result.Succeeded)
        {
            Nav.NavigateTo("/");
            return;
        }

        Authorized = true;
        await OnAuthorizedParametersSetAsync();
    }

    // Hook for a subclass's own loading logic — only ever called once authorization for
    // ParsedGuildId has succeeded; re-runs on every subsequent parameter change (e.g.
    // FeatureSettings.razor navigating between feature/audience segments on the same
    // mounted instance).
    protected virtual Task OnAuthorizedParametersSetAsync() => Task.CompletedTask;
}
