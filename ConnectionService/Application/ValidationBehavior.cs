using FluentValidation;
using MediatR;

namespace ConnectionService.Application;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = validators.Select(v => v.Validate(request)).SelectMany(r => r.Errors).Where(e => e is not null).ToList();
        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
