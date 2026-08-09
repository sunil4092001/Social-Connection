using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Repository;
using IdentityService.Services.Interface;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IAuthRepository _repository;

        public ProfileService(IAuthRepository repository)
        {
            _repository = repository;
        }
        public async Task<UserProfileDto> GetProfileAsync(Guid UserId)
        {
           
            var user = await _repository.GetProfile(UserId);

            if (user == null)
            {
                throw new Exception("User Does not exisit");
            }

            var userDto = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                //FirstName = user.FirstName,
                //LastName = user.LastName,
                //PhoneNumber = user.PhoneNumber,
            };
            return userDto;
        }

        //public async Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> UserIds, CancellationToken cancellationToken)
        //{

        //    return  await _repository.GetUsersProfileAsync(UserIds, cancellationToken);
        //}

        public Task SaveChangesAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<UserProfileDto> UpdateProfileAsync(UserProfileDto profileDto)
        {

            var user = new User
            {
                Id = profileDto.Id,
                Email = profileDto.Email,
                //FirstName = profileDto.FirstName,
                //LastName = profileDto.LastName,
                //PhoneNumber = profileDto.PhoneNumber
            };

            var updatedUser =  await _repository.UpdateProfileAsync(user);

            return new UserProfileDto
            {
                Id = updatedUser.Id,
                //FirstName = updatedUser.FirstName,
                //LastName = updatedUser.LastName,
                Email = updatedUser.Email,
                //PhoneNumber = updatedUser.PhoneNumber
            };

        }

       
    }
}
