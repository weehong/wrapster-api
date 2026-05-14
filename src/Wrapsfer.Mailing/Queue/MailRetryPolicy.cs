using Polly;
using Polly.Retry;
using Wrapsfer.Mailing.Abstractions;

namespace Wrapsfer.Mailing.Queue;

internal static class MailRetryPolicy
{
    public static ResiliencePipeline<MailSendOutcome> Build(int maxRetries, TimeSpan initialBackoff) =>
        new ResiliencePipelineBuilder<MailSendOutcome>()
            .AddRetry(new RetryStrategyOptions<MailSendOutcome>
            {
                ShouldHandle = new PredicateBuilder<MailSendOutcome>()
                    .HandleResult(static outcome => outcome is { IsSuccess: false, IsTransient: true }),
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = initialBackoff
            })
            .Build();
}
