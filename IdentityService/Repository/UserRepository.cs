using System;
using IdentityService.Data;
using IdentityService.DTOs;
using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Repository
{
    public class UserRepository : IAuthRepository
    {
        private readonly IdentityDbContext _context;
        private readonly IUserServiceClient _userServiceClient;


        public UserRepository(IdentityDbContext context, IUserServiceClient userServiceClient)
        {
            _context = context;
        }

        #region ==== Register Login=======

        public async Task RegisterAsync(User request)
        {
            await _context.AddAsync(request);
            await _context.SaveChangesAsync();
           
        }
      
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<RefreshToken> GetRefreshTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == token);
        }

        public async Task AddRefreshTokenAsync(RefreshToken token)
        {
            await _context.RefreshTokens.AddAsync(token);
        }

        #endregion

        #region ==== User Profile detail =======


        public  Task<User?> GetProfile(Guid userID)
        {
            var userDetail = _context.Users.FirstOrDefaultAsync(x => x.Id == userID);

            return userDetail;
        }



        #endregion
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<User> UpdateProfileAsync(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user.Email));
            }
            
            var existingUser = await _context.Users.FirstOrDefaultAsync(x => x.Id == user.Id);

            if (existingUser == null)
            {
                throw new KeyNotFoundException("User not found.");
            }
            //existingUser.FirstName = user.FirstName;
            //existingUser.LastName = user.LastName;
            //existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.Email = user.Email;

            await _context.SaveChangesAsync();

            return existingUser;
        }

        //public async Task<List<UserSummaryDto>> GetUsersProfileAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
        //{
        //    return await _context.Users
        //        .Where(u => userIds.Contains(u.Id))
        //        .Select (u => new UserSummaryDto(
        //            u.Id
        //            //u.FirstName,
        //            //u.LastName
        //            )).ToListAsync(cancellationToken);
        //}
    }
}
