using FluentValidation;
using MediatR;
namespace RequirementService.Application;
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)))).SelectMany(r => r.Errors).Where(e => e is not null).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);
        return await next();
    }
}
