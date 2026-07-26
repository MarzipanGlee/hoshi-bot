using HoshiBot.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Shared;

// Base for the STFC admin Create pages: the form-bound Entity plus the Add + SaveChanges +
// back-to-list submit that was verbatim across them. Pages that load select-list options
// override OnInitializedAsync (the Entity default still comes from OnInitialized, which runs
// first); pages with a pre-insert guard or upsert keep their own submit handler and call
// AddAsync (or replace it) from there.
public abstract class EntityCreatePageBase<TEntity> : ComponentBase where TEntity : class
{
    [Inject] protected IDbContextFactory<HoshiBotDbContext> DbFactory { get; set; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = null!;

    [SupplyParameterFromForm]
    protected TEntity Entity { get; set; } = default!;

    protected abstract string ListHref { get; }

    protected abstract TEntity CreateNew();

    protected override void OnInitialized() => Entity ??= CreateNew();

    protected async Task AddAsync()
    {
        using var context = DbFactory.CreateDbContext();
        context.Add(Entity);
        await context.SaveChangesAsync();
        NavigationManager.NavigateTo(ListHref);
    }
}
