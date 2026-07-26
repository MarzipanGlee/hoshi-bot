using HoshiBot.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Shared;

// Base for the STFC admin Edit pages: query-string Id + form-bound Entity, the load-or-404
// OnInitializedAsync, and the Attach/Modified save with the concurrency-conflict existence
// re-check. Pages that also load select-list options override OnInitializedAsync, fetch them,
// then call base.OnInitializedAsync() — the options must load on every render, including the
// failed-validation re-render where Entity comes from the form and LoadAsync is skipped.
public abstract class EntityEditPageBase<TEntity, TKey> : ComponentBase where TEntity : class
{
    [Inject] protected IDbContextFactory<HoshiBotDbContext> DbFactory { get; set; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = null!;

    [SupplyParameterFromQuery]
    protected TKey Id { get; set; } = default!;

    [SupplyParameterFromForm]
    protected TEntity? Entity { get; set; }

    protected abstract string ListHref { get; }

    protected abstract Task<TEntity?> LoadAsync(HoshiBotDbContext context, TKey id);

    protected abstract Task<bool> ExistsAsync(HoshiBotDbContext context, TKey id);

    protected override async Task OnInitializedAsync()
    {
        using var context = DbFactory.CreateDbContext();
        Entity ??= await LoadAsync(context, Id);

        if (Entity is null)
        {
            NavigationManager.NotFound();
        }
    }

    protected async Task SaveAsync()
    {
        using var context = DbFactory.CreateDbContext();
        context.Attach(Entity!).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // The existence re-check runs on a fresh context: the failed one still tracks the
            // conflicted entity, and the pre-extraction pages created a new context here too.
            using var checkContext = DbFactory.CreateDbContext();
            if (!await ExistsAsync(checkContext, Id))
            {
                NavigationManager.NotFound();
            }
            else
            {
                throw;
            }
        }

        NavigationManager.NavigateTo(ListHref);
    }
}
