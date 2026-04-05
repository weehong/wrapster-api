using Wrapster.Domain.Common;

namespace Wrapster.Application.Abstractions.Email;

public interface IEmailService
{
    Task<Result> SendAsync<TTemplate>(string to, TTemplate template, CancellationToken cancellationToken = default)
        where TTemplate : IEmailTemplate;
}
