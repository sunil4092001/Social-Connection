using MediatR;
using UserService.Application.Dtos.ServiceCall;
using UserService.Domain;
using UserService.Repositories;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UserService.Application.Profiles.ServiceCall
{
    //public sealed class GetUsersProfileHandler (IUserProfileRepository repository): IRequestHandler<GetUsersProfile, List<UserSummaryDto>>
    public sealed class GetUsersProfileHandler(IUserProfileRepository repository) : IRequestHandler<GetUsersProfile, List<UserSummaryDto>>
    {
        public async Task<List<UserSummaryDto>> Handle(GetUsersProfile request, CancellationToken cancellationToken)
        {
            
           return await repository.GetUsersProfileAsync(request.ids, cancellationToken);
            //return profile.ToDto();
        }
    }
}
