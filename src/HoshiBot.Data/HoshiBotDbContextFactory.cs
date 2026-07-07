using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HoshiBot.Data;

public class HoshiBotDbContextFactory : IDesignTimeDbContextFactory<HoshiBotDbContext>
{
    public HoshiBotDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HOSHIBOT_CONNECTIONSTRING")
            ?? "Host=localhost;Database=hoshibot;Username=hoshibot;Password=hoshibot;";

        var optionsBuilder = new DbContextOptionsBuilder<HoshiBotDbContext>()
            .UseNpgsql(connectionString);

        return new HoshiBotDbContext(optionsBuilder.Options);
    }
}
