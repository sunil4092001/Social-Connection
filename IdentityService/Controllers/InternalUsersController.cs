using IdentityService.DTOs.User;
using IdentityService.Entities;
using IdentityService.Services;
using IdentityService.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers
{

    //not using
    [ApiController]
    [Route("internal/users")]
    public class InternalUsersController : ControllerBase
    {
        private readonly IProfileService _profileService;
        public InternalUsersController(IProfileService profileService)
        {
         _profileService = profileService;   
        }

        [HttpPost("batch")]
        public async Task<ActionResult<List<UserSummaryDto>>> GetUsers([FromBody] List<Guid> ids, CancellationToken cancellationToken)
        {
           

            var users = new List<UserSummaryDto>();

             //users = await _profileService.GetUsersProfileAsync(ids, cancellationToken);

            return Ok(users);
        }
    }
}
