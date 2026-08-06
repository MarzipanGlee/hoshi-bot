using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

// This drives a kill switch, so both directions matter: failing to trip it lets a revoked token run
// the invalid-request counter up to a Cloudflare ban, and tripping it wrongly stops the entire bot.
public class DiscordApiHealthTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_token_switch_starts_off_and_is_one_way()
    {
        var health = new DiscordApiHealth();
        Assert.False(health.TokenInvalid);

        health.MarkTokenInvalid();
        Assert.True(health.TokenInvalid);

        // Nothing clears it — recovering means a new token, which means a restart.
        health.MarkTokenInvalid();
        Assert.True(health.TokenInvalid);
    }

    [Fact]
    public void Invalid_requests_accumulate_within_the_window()
    {
        var health = new DiscordApiHealth();

        Assert.Equal(1, health.RecordInvalidRequest(T0));
        Assert.Equal(2, health.RecordInvalidRequest(T0.AddSeconds(30)));
        Assert.Equal(3, health.RecordInvalidRequest(T0.AddMinutes(9)));
    }

    // The window has to roll, not reset: Discord counts any 10 minutes, so a steady trickle must not
    // look like zero just because no single burst crossed the line.
    [Fact]
    public void Requests_older_than_ten_minutes_drop_out_of_the_count()
    {
        var health = new DiscordApiHealth();
        health.RecordInvalidRequest(T0);
        health.RecordInvalidRequest(T0.AddMinutes(1));

        // At T0+10:30 the cutoff is T0+0:30, so only the first has aged out — the second is still
        // 9½ minutes old and very much inside the window Discord measures.
        Assert.Equal(2, health.RecordInvalidRequest(T0.AddMinutes(10).AddSeconds(30)));

        // At T0+11:30 the cutoff has passed both of the originals.
        Assert.Equal(2, health.RecordInvalidRequest(T0.AddMinutes(11).AddSeconds(30)));
    }

    [Fact]
    public void No_warning_below_a_tenth_of_the_ceiling()
    {
        var health = new DiscordApiHealth();
        Assert.False(health.ShouldWarn(1));
        Assert.False(health.ShouldWarn(999));
    }

    // Once it is worth saying, say it once per thousand — not on every request, which at these
    // volumes would itself be the problem.
    [Fact]
    public void Warns_once_per_thousand_past_the_threshold()
    {
        var health = new DiscordApiHealth();

        Assert.True(health.ShouldWarn(1_000));
        Assert.False(health.ShouldWarn(1_400));
        Assert.False(health.ShouldWarn(1_999));
        Assert.True(health.ShouldWarn(2_000));
        Assert.True(health.ShouldWarn(3_100));
    }

    // Recovery: once the count falls back to healthy, a later spike is worth reporting again rather
    // than being suppressed because a previous incident already used up that step.
    [Fact]
    public void Falling_back_below_the_threshold_re_arms_the_warning()
    {
        var health = new DiscordApiHealth();
        Assert.True(health.ShouldWarn(2_000));

        Assert.False(health.ShouldWarn(10));
        Assert.True(health.ShouldWarn(1_000));
    }
}
