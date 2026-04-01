using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IValidationResult<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IValidator<TRequest>> validatorList = validators as IReadOnlyList<IValidator<TRequest>>
                                                            ?? [..validators];

        if (validatorList.Count == 0)
        {
            return await next(cancellationToken);
        }

        ValidationContext<TRequest> context = new(request);
        ValidationResult[] validationResults =
            await Task.WhenAll(validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));
        List<ValidationFailure> failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            string errorMessage = string.Join(";", failures.Select(f => f.ErrorMessage));
            Error error = new("Validation", errorMessage, ErrorType.Validation);

            logger.LogWarning("Validation failed for {RequestName}: {ErrorMessage}", typeof(TRequest).Name,
                errorMessage);

            return TResponse.Failure(error);
        }

        return await next(cancellationToken);
    }
}
