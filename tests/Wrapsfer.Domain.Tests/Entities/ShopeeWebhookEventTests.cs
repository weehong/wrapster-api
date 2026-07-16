using FluentAssertions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Tests.Entities;

public sealed class ShopeeWebhookEventTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private static ShopeeWebhookEvent CreateEvent()
    {
        Result<ShopeeWebhookEvent> result = ShopeeWebhookEvent.Create(
            123456, 3, "abc123hash", "{\"code\":3}", Now);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_StartsPending()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Pending);
        evt.AttemptCount.Should().Be(0);
        evt.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithBlankMessageKey_Fails()
    {
        Result<ShopeeWebhookEvent> result = ShopeeWebhookEvent.Create(123456, 3, " ", "{}", Now);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkProcessed_SetsStatusAndTimestamp()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.MarkProcessed(Now.AddSeconds(5));
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Processed);
        evt.ProcessedAtUtc.Should().Be(Now.AddSeconds(5));
    }

    [Fact]
    public void MarkFailed_BacksOffExponentially()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.MarkFailed("boom", Now, maxAttempts: 5);
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Failed);
        evt.AttemptCount.Should().Be(1);
        evt.NextAttemptAt.Should().Be(Now.AddMinutes(1));
        evt.MarkFailed("boom", Now, maxAttempts: 5);
        evt.NextAttemptAt.Should().Be(Now.AddMinutes(2));
    }

    [Fact]
    public void MarkFailed_AfterMaxAttempts_StopsRetrying()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        for (int i = 0; i < 5; i++)
        {
            evt.MarkFailed("boom", Now, maxAttempts: 5);
        }
        evt.AttemptCount.Should().Be(5);
        evt.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void MarkIgnored_SetsStatus()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.MarkIgnored();
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Ignored);
    }
}
