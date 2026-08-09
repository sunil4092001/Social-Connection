using System.Security.Claims;
using BuildingBlocks.Response;
using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers
{
    // not used
    [ApiController]
    [Route("api/profile")]    
    public class ProfileController : ControllerBase
    {

        private readonly ProfileService _profileService;


        public ProfileController(ProfileService profileService)
        {
            _profileService = profileService;
        }

        [Authorize]
        [HttpGet("data")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var response = await _profileService.GetProfileAsync(Guid.Parse(userId));

            return Ok(ApiResponseFactory.Success(
                response,
                "User Profile"
                ));
        }

      
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfileDto profileDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedProfile =
                await _profileService.UpdateProfileAsync(profileDto);

            return Ok(updatedProfile);
        }

        //{
        //  "id": 1,
        //  "email": "sunil@test.com",
        //  "firstName": "Sunil",
        //  "lastName": "Kumar",
        //  "phoneNumber": "9876543210"
        //}
    }


}
