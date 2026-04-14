using Wrapster.Mailing.Abstractions;

namespace Wrapster.Mailing.Tests.Abstractions;

public class MailSendOutcomeTests
{
    [Fact]
    public void Success_MarksSuccessAndCarriesProviderId()
    {
        MailSendOutcome outcome = MailSendOutcome.Success("msg-123");

        outcome.IsSuccess.Should().BeTrue();
        outcome.IsTransient.Should().BeFalse();
        outcome.ProviderMessageId.Should().Be("msg-123");
    }

    [Fact]
    public void TransientFailure_MarksTransientAndCarriesError()
    {
        MailSendOutcome outcome = MailSendOutcome.TransientFailure("code", "desc");

        outcome.IsSuccess.Should().BeFalse();
        outcome.IsTransient.Should().BeTrue();
        outcome.ErrorCode.Should().Be("code");
        outcome.ErrorDescription.Should().Be("desc");
    }

    [Fact]
    public void PermanentFailure_DoesNotMarkTransient()
    {
        MailSendOutcome outcome = MailSendOutcome.PermanentFailure("code", "desc");

        outcome.IsSuccess.Should().BeFalse();
        outcome.IsTransient.Should().BeFalse();
    }
}
