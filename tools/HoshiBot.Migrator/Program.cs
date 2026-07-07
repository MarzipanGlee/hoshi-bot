using HoshiBot.Data;
using Microsoft.EntityFrameworkCore;

var connectionString = Environment.GetEnvironmentVariable("HOSHIBOT_CONNECTIONSTRING")
    ?? throw new InvalidOperationException("Set the HOSHIBOT_CONNECTIONSTRING environment variable to the target PostgreSQL database.");

var optionsBuilder = new DbContextOptionsBuilder<HoshiBotDbContext>().UseNpgsql(connectionString);
await using var db = new HoshiBotDbContext(optionsBuilder.Options);

Console.WriteLine("Applying pending EF Core migrations...");
await db.Database.MigrateAsync();
Console.WriteLine("Schema is up to date.");

// No data import steps are registered yet. Add one per feature area once the
// corresponding YAGPDB KV export (Definitions, Menus, Announcements, Members,
// per-user Raids/ShieldReminder, ...) has been pulled from the dashboard/API.
Console.WriteLine("No data import steps are registered yet.");
