using FluentValidation;

namespace ConnectionService.Application.Commands.RequestConnection
{
    public class RequestConnectionValidator : AbstractValidator<RequestConnectionCommand>
    {
        public RequestConnectionValidator()
        {
            RuleFor(command => command.RequesterUserId).NotEmpty();
            RuleFor(command => command.AddresseeUserId).NotEmpty();
            RuleFor(command => command).Must(command => command.RequesterUserId != command.AddresseeUserId)
                .WithMessage("Requester and addressee must be different users.");
        }
    }
}
