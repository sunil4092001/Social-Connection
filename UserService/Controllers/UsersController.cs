using BuildingBlocks.Common;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Dtos;
using UserService.Application.Dtos.ServiceCall;
using UserService.Application.Profiles;
using UserService.Application.Profiles.ServiceCall;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{

    [HttpPost]
    public async Task<ActionResult<Application.Dtos.UpdateUserProfileCommand>> Create(CreateUserProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await mediator.Send(
            new CreateUserProfileCommand(request.UserId, request.FirstName, request.LastName,request.DisplayName, request.Headline, request.About, request.Location),
            cancellationToken);

        return CreatedAtAction(nameof(GetProfileById), new { id = profile.Id }, profile);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<Application.Dtos.UpdateUserProfileCommand>> GetProfile(CancellationToken cancellationToken)
    {

        var userId = User.GetUserId();
        var profile = await mediator.Send(new GetUserProfileByIdQuery(userId), cancellationToken);
        return profile is null ? NotFound() : Ok(ApiResponseFactory.Success(
                profile,
                "Login User Profile Detail"
            ));
    }

    //[Authorize]
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<Application.Dtos.UpdateUserProfileCommand>> GetProfileById(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await mediator.Send(new GetUserProfileByIdQuery(userId), cancellationToken);
        return profile is null ? NotFound() : Ok(ApiResponseFactory.Success(
                profile,
                "Profile Detail"
            ));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<ApiResponse<Application.Dtos.UpdateUserProfileCommand>>> UpdateProfile(Application.Dtos.UpdateUserProfileCommand request , CancellationToken cancellationToken)
    {

        
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId.ToString()))
             return Unauthorized(ApiResponseFactory.Failure<Application.Dtos.UpdateUserProfileCommand>("User Are Not Authrized",null));
             //Unauthorized();


        var profile = await mediator.Send(
                new Application.Profiles.UpdateProfileCommand(userId, request.FirstName, request.LastName, request.DisplayName, request.Headline, request.About, request.Location),
                cancellationToken);
        return profile is null ? NotFound() : Ok(ApiResponseFactory.Success(
                profile,
                "Profile Detail"
            ));
    }



    //[HttpGet("{id:guid}")]
    //public async Task<ActionResult<Application.Dtos.UpdateUserProfileCommand>> GetById(Guid id, CancellationToken cancellationToken)
    //{
    //    var profile = await mediator.Send(new GetUserProfileByIdQuery(id), cancellationToken);
    //    return profile is null ? NotFound() : Ok(ApiResponseFactory.Success(
    //            profile,
    //            "Profile search Detail"
    //        ));
    //}

    //[HttpGet]
    //public async Task<ActionResult<IReadOnlyList<Application.Dtos.UpdateUserProfileCommand>>> Search([FromQuery] string? term, CancellationToken cancellationToken)
    //{
    //    var profiles = await mediator.Send(new SearchUserProfilesQuery(term), cancellationToken);
    //    return Ok(profiles);
    //}

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<List<UserSummaryDto>>>> GetUsers([FromBody] List<Guid> ids, CancellationToken cancellationToken)
    {
        var users = new List<UserSummaryDto>();
        users = await mediator.Send(new GetUsersProfile(ids), cancellationToken);

        return Ok(users);
    }



}
