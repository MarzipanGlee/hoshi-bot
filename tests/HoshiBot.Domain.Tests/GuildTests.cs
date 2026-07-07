using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class GuildTests
{
    [Fact]
    public void NewGuild_HasNoAllianceLinksOrMembers()
    {
        var guild = new DiscordGuild { Id = 1, Name = "Test Guild" };

        Assert.Empty(guild.AllianceLinks);
        Assert.Empty(guild.Members);
    }
}
