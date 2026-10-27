using FluentValidation;

namespace UserService.Application.Profiles.ServiceCall;

/// <summary>Keeps the batch lookup bounded so one request cannot ask for the whole user table.</summary>
public sealed class GetUsersProfileValidator : AbstractValidator<GetUsersProfile>
{
    public const int MaxIds = 200;

    public GetUsersProfileValidator()
    {
        RuleFor(query => query.ids).NotNull().Must(ids => ids.Count <= MaxIds).WithMessage($"At most {MaxIds} user ids can be requested at once.");
        RuleForEach(query => query.ids).NotEmpty();
    }
}
