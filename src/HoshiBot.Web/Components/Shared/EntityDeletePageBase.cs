using HoshiBot.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Shared;

// Base for the STFC admin Delete pages: query-string Id, the load-or-404 OnInitializedAsync
// (LoadAsync carries each page's Includes for the confirmation fields), and the Remove +
// SaveChanges + back-to-list confirm handler wired to <DeleteConfirmation>.
public abstract class EntityDeletePageBase<TEntity, TKey> : ComponentBase where TEntity : class
{
    [Inject] protected IDbContextFactory<HoshiBotDbContext> DbFactory { get; set; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = null!;

    [SupplyParameterFromQuery]
    protected TKey Id { get; set; } = default!;

    protected TEntity? Entity { get; set; }

    protected abstract string ListHref { get; }

    protected abstract Task<TEntity?> LoadAsync(HoshiBotDbContext context, TKey id);

    protected override async Task OnInitializedAsync()
    {
        using var context = DbFactory.CreateDbContext();
        Entity = await LoadAsync(context, Id);

        if (Entity is null)
        {
            NavigationManager.NotFound();
        }
    }

    protected async Task DeleteAsync()
    {
        using var context = DbFactory.CreateDbContext();
        context.Remove(Entity!);
        await context.SaveChangesAsync();
        NavigationManager.NavigateTo(ListHref);
    }
}
