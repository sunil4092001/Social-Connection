using MediatR;
using UserService.Application.Dtos.ServiceCall;

namespace UserService.Application.Profiles.ServiceCall
{

    public sealed record GetUsersProfile(List<Guid> ids) : IRequest<List<UserSummaryDto?>>;

}
