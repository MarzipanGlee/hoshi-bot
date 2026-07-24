using HoshiBot.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Shared;

// Base for the read-only QuickGrid list pages (Stfc/**/Index, Database/*) that keep one long-lived
// DbContext for the page's lifetime — extracts the identical field + OnInitialized + IAsyncDisposable
// boilerplate that was verbatim across ~35 pages. Subclasses bind their QuickGrid's Items to
// `Context.<DbSet>`.
public abstract class DbContextPageBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IDbContextFactory<HoshiBotDbContext> DbFactory { get; set; } = null!;

    protected HoshiBotDbContext Context = default!;

    protected override void OnInitialized() => Context = DbFactory.CreateDbContext();

    public async ValueTask DisposeAsync()
    {
        if (Context is not null)
            await Context.DisposeAsync();
    }
}
