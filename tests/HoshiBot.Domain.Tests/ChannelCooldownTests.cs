using HoshiBot.Domain.Entities;
using Xunit;

namespace HoshiBot.Domain.Tests;

// This decides whether a Discord call happens at all, so both failure directions cost something:
// too eager and we're back to hammering a broken channel, too sticky and work stops silently.
public class ChannelCooldownTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private const ulong Channel = 100;
    private const ulong Other = 200;

    [Fact]
    public void A_channel_that_has_not_failed_is_never_cooling_down()
    {
        var cooldown = new ChannelCooldown();

        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0));
    }

    [Fact]
    public void A_failure_blocks_the_next_minute_and_then_lets_go()
    {
        var cooldown = new ChannelCooldown();
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);

        Assert.True(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddSeconds(59)));
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(1)));
    }

    // 1 → 5 → 15 → 30 minutes: a channel that keeps failing gets asked about less and less.
    [Fact]
    public void Repeated_failures_escalate_the_wait()
    {
        var cooldown = new ChannelCooldown();

        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0.AddMinutes(1));
        Assert.True(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(5)));
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(6)));

        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0.AddMinutes(6));
        Assert.True(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(20)));
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(21)));
    }

    // The property that matters most: a cooldown must never become a permanent silent block. That
    // is the exact failure mode this whole change exists to remove, and it would be an easy one to
    // reintroduce by "just" making the ladder open-ended.
    [Fact]
    public void The_wait_caps_and_never_becomes_permanent()
    {
        var cooldown = new ChannelCooldown();
        for (var i = 0; i < 500; i++)
            cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);

        Assert.True(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(29)));
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(30)));
    }

    [Fact]
    public void Success_clears_the_cooldown_and_resets_the_ladder()
    {
        var cooldown = new ChannelCooldown();
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);

        cooldown.RecordSuccess(Channel, BotAction.SendAlert);
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0));

        // Back to the bottom of the ladder, not wherever it had climbed to.
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.SendAlert, T0.AddMinutes(1)));
    }

    [Fact]
    public void Channels_and_actions_are_tracked_independently()
    {
        var cooldown = new ChannelCooldown();
        cooldown.RecordFailure(Channel, BotAction.SendAlert, T0);

        // A different channel is unaffected...
        Assert.False(cooldown.IsCoolingDown(Other, BotAction.SendAlert, T0));

        // ...and so is a different operation on the same channel: a failing thread delete must not
        // silence an alert post in the same place.
        Assert.False(cooldown.IsCoolingDown(Channel, BotAction.RemoveThread, T0));
    }
}
