using BuildingBlocks.Common;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Dtos;
using UserService.Application.Profiles;
using UserService.Application.Profiles.ServiceCall;
using UserService.Application.Skills;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    // This endpoint is for the IdentityService registration handoff only. External callers must use /me.
    // It lives outside /api (absolute route) so the API gateway, which only forwards /api/..., never exposes it.
    [HttpPost("/internal/users/profiles")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<UpdateUserProfileCommand>> CreateInternal(
        CreateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var serviceKey = configuration["InternalService:UserServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey) || !Request.Headers.TryGetValue("X-Internal-Service-Key", out var suppliedKey) || suppliedKey != serviceKey)
        {
            return Unauthorized(ApiResponseFactory.Failure<UpdateUserProfileCommand>("Invalid internal service credentials."));
        }

        var profile = await mediator.Send(new CreateUserProfileCommand(
            request.UserId, request.FirstName, request.LastName, request.DisplayName, request.Headline,
            request.About, request.Location, request.UserType, request.ProfessionalType,
            request.ExperienceYears, request.AvailabilityStatus), cancellationToken);

        return CreatedAtAction(nameof(GetProfileById), new { userId = profile.Id }, profile);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UpdateUserProfileCommand>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await mediator.Send(new GetUserProfileByIdQuery(userId), cancellationToken);
        return profile is null ? NotFound() : Ok(ApiResponseFactory.Success(profile, "Logged-in user profile detail."));
    }

    [Authorize]
    [HttpPost("me")]
    public async Task<ActionResult<UpdateUserProfileCommand>> CreateMyProfile(
        CreateMyProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var existingProfile = await mediator.Send(new GetUserProfileByIdQuery(userId), cancellationToken);
        if (existingProfile is not null)
        {
            return Ok(ApiResponseFactory.Success(existingProfile, "Profile already exists."));
        }

        var profile = await mediator.Send(new CreateUserProfileCommand(
            userId,
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.Headline,
            request.About,
            request.Location,
            request.UserType,
            request.ProfessionalType,
            request.ExperienceYears,
            request.AvailabilityStatus), cancellationToken);

        return CreatedAtAction(
            nameof(GetProfileById),
            new { userId = profile.Id },
            ApiResponseFactory.Success(profile, "Profile created."));
    }

    [Authorize]
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UpdateUserProfileCommand>> GetProfileById(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await mediator.Send(new GetUserProfileByIdQuery(userId), cancellationToken);
        return profile is null ? NotFound() : Ok(ApiResponseFactory.Success(profile, "Profile detail."));
    }

        [Authorize]
        [HttpGet("search")]
        public async Task<ActionResult<IReadOnlyList<UpdateUserProfileCommand>>> Search(
            [FromQuery] string? term,
            [FromQuery] UserService.Domain.ProfessionalType? professionalType,
            [FromQuery] string? location,
            [FromQuery] string? skill,
            [FromQuery] int? minimumExperienceYears,
            [FromQuery] UserService.Domain.AvailabilityStatus? availabilityStatus,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            if (userId == Guid.Empty) return Unauthorized();
            var currentProfile = await mediator.Send(new GetUserProfileByIdQuery(userId), cancellationToken);
            if (currentProfile is null) return NotFound();
            if (currentProfile.UserType != UserService.Domain.UserType.CUSTOMER) return Forbid();

            return Ok(ApiResponseFactory.Success(
                await mediator.Send(new SearchUserProfilesQuery(
                    term, professionalType, location, skill, minimumExperienceYears, availabilityStatus), cancellationToken),
                "Profiles."));
        }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UpdateUserProfileCommand>> UpdateProfile(UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var profile = await mediator.Send(new UpdateProfileCommand(
            userId, request.FirstName, request.LastName, request.DisplayName, request.Headline,
            request.About, request.Location, request.ProfessionalType, request.ExperienceYears,
            request.AvailabilityStatus, request.ProfileImageUrl), cancellationToken);

        return Ok(ApiResponseFactory.Success(profile, "Profile updated."));
    }

    [HttpGet("/api/skills")]
    public async Task<ActionResult<IReadOnlyList<SkillDto>>> GetSkills(CancellationToken cancellationToken) =>
        Ok(ApiResponseFactory.Success(await mediator.Send(new GetSkillsQuery(), cancellationToken), "Skills."));

    [Authorize]
    [HttpPost("{userId:guid}/skills")]
    public async Task<ActionResult<ProfileSkillDto>> AddSkill(Guid userId, AddProfessionalSkillRequest request, CancellationToken cancellationToken)
    {
        if (userId != User.GetUserId()) return Forbid();
        var result = await mediator.Send(new AddProfessionalSkillCommand(userId, request.SkillId, request.ExperienceYears, request.SkillLevel), cancellationToken);
        return Ok(ApiResponseFactory.Success(result, "Skill added."));
    }

    [Authorize]
    [HttpDelete("{userId:guid}/skills/{skillId:guid}")]
    public async Task<IActionResult> RemoveSkill(Guid userId, Guid skillId, CancellationToken cancellationToken)
    {
        if (userId != User.GetUserId()) return Forbid();
        return await mediator.Send(new RemoveProfessionalSkillCommand(userId, skillId), cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Names for a list of user ids (used by other services to decorate lists). Requires a signed-in caller.</summary>
    [Authorize]
    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<UserService.Application.Dtos.ServiceCall.UserSummaryDto>>> GetUsers(
        [FromBody] List<Guid> ids, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUsersProfile(ids), cancellationToken));
}
