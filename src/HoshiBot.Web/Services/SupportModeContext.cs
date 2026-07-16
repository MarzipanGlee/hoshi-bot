using HoshiBot.Data;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// Scoped per-circuit "is support mode on for me right now" state, backing the top-row
// Support switch. DB-backed (the GlobalAdmin.SupportMode column) rather than
// ProtectedLocalStorage-backed like CurrentGuildContext, so the authorization pipeline
// (GuildAdminPageBase -> AuthorizeAsync, via SupportModeGuildAdminHandler) can read the
// same truth server-side before the interactive circuit exists — that's what lets a
// support admin open a non-owned guild page via direct navigation / refresh.
//
// IsActive is only ever true for a real global admin: a non-admin has no GlobalAdmins row,
// so InitializeAsync/SetAsync leave it false. Consumers can treat IsActive == true as
// "global admin with support on" without a separate admin check.
public class SupportModeContext(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public bool IsActive { get; private set; }
    public event Action? Changed;

    // Seed the state from the DB. MainLayout calls this before CurrentGuildContext is
    // initialized (so its SetAsync validation, via GuildAccessService, already sees the right
    // support-mode state); data-loading pages that can run before MainLayout on a fresh load
    // (e.g. the dashboard) may call it too. Idempotent: only fires Changed when the value
    // actually changes, so repeat calls in the same circuit don't trigger redundant reloads
    // or a Changed -> reload -> Changed loop.
    public async Task InitializeAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var admin = await db.GlobalAdmins.FindAsync(userId);
        var value = admin?.SupportMode ?? false;
        if (value == IsActive)
            return;

        IsActive = value;
        Changed?.Invoke();
    }

    public async Task SetAsync(ulong userId, bool value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var admin = await db.GlobalAdmins.FindAsync(userId);
        if (admin is null)
            return; // Not a global admin — nothing to persist against.

        admin.SupportMode = value;
        await db.SaveChangesAsync();

        IsActive = value;
        Changed?.Invoke();
    }
}
