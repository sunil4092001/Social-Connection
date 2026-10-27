using BuildingBlocks.Common;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequirementService.Application;
using RequirementService.Domain;
using RequirementService.Infrastructure;

namespace RequirementService.Controllers;

/// <summary>
/// The user id always comes from the JWT. The user type (CUSTOMER / PROFESSIONAL) is owned by UserService and is
/// checked over HTTP; ownership and status rules are enforced by the handlers and the Requirement entity.
/// </summary>
[ApiController]
[Authorize]
[Route("api/requirements")]
public sealed class RequirementsController(IMediator mediator, IUserProfileClient profiles) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RequirementDto>> Create([FromBody] CreateRequirementRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        // Only customers post requirements.
        if (!await IsCustomerAsync(userId, cancellationToken)) return Forbid();
        var requirement = await mediator.Send(new CreateRequirementCommand(userId, request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = requirement.Id }, ApiResponseFactory.Success(requirement, "Requirement created."));
    }

    /// <summary>Public search. Responses are hidden unless the caller owns the requirement (see RequirementDto).</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RequirementDto>>> Search([FromQuery] string? professionalType, [FromQuery] string? location, [FromQuery] RequirementStatus? status, [FromQuery] bool mine, CancellationToken cancellationToken)
    {
        var viewerId = User.GetUserId();
        Guid? customerId = null;
        if (mine)
        {
            if (viewerId == Guid.Empty) return Unauthorized();
            if (!await IsCustomerAsync(viewerId, cancellationToken)) return Forbid();
            customerId = viewerId;
        }
        var requirements = await mediator.Send(new GetRequirementsQuery(viewerId, customerId, professionalType, location, status), cancellationToken);
        return Ok(ApiResponseFactory.Success(requirements, "Requirements."));
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<IReadOnlyList<RecommendedRequirementDto>>> Recommended(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var professional = await profiles.GetProfessionalMatchProfileAsync(userId, cancellationToken);
        if (professional is null) return NotFound();
        if (!string.Equals(professional.UserType, "PROFESSIONAL", StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (string.IsNullOrWhiteSpace(professional.ProfessionalType)) return Forbid();

        var recommendations = await mediator.Send(new GetRecommendedRequirementsQuery(professional), cancellationToken);
        return Ok(ApiResponseFactory.Success(recommendations, "Recommended requirements."));
    }

    [HttpGet("responses/me")]
    public async Task<ActionResult<IReadOnlyList<RequirementDto>>> MyResponses(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await profiles.HasTypeAsync(userId, "PROFESSIONAL", cancellationToken)) return Forbid();

        var requirements = await mediator.Send(new GetProfessionalResponsesQuery(userId), cancellationToken);
        return Ok(ApiResponseFactory.Success(requirements, "My requirement responses."));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequirementDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var requirement = await mediator.Send(new GetRequirementQuery(id, User.GetUserId()), cancellationToken);
        return requirement is null ? NotFound() : Ok(ApiResponseFactory.Success(requirement, "Requirement detail."));
    }

    /// <summary>Edits the details of an OPEN requirement. The status cannot be changed here.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RequirementDto>> Update(Guid id, UpdateRequirementRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await IsCustomerAsync(userId, cancellationToken)) return Forbid();
        var requirement = await mediator.Send(new UpdateRequirementCommand(userId, id, request), cancellationToken);
        return Ok(ApiResponseFactory.Success(requirement, "Requirement updated."));
    }

    /// <summary>Cancels an OPEN or IN_PROGRESS requirement and notifies the professionals who responded.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await IsCustomerAsync(userId, cancellationToken)) return Forbid();
        await mediator.Send(new CancelRequirementCommand(userId, id), cancellationToken);
        return NoContent();
    }

    /// <summary>Marks an IN_PROGRESS requirement as COMPLETED.</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<RequirementDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await IsCustomerAsync(userId, cancellationToken)) return Forbid();
        var requirement = await mediator.Send(new CompleteRequirementCommand(userId, id), cancellationToken);
        return Ok(ApiResponseFactory.Success(requirement, "Requirement completed."));
    }

    /// <summary>A professional of the matching trade expresses interest in an OPEN requirement.</summary>
    [HttpPost("{id:guid}/interest")]
    public async Task<ActionResult<RequirementResponseDto>> Respond(Guid id, RespondToRequirementRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var professional = await profiles.GetProfessionalMatchProfileAsync(userId, cancellationToken);
        if (professional is null
            || !string.Equals(professional.UserType, "PROFESSIONAL", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(professional.ProfessionalType))
        {
            return Forbid();
        }

        var response = await mediator.Send(new RespondToRequirementCommand(userId, professional.ProfessionalType, id, request.Message), cancellationToken);
        return Ok(ApiResponseFactory.Success(response, "Interest recorded."));
    }

    /// <summary>Owner only: the professionals who responded, with their names.</summary>
    [HttpGet("{id:guid}/responses")]
    public async Task<ActionResult<IReadOnlyList<RequirementResponseDto>>> Responses(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var responses = await mediator.Send(new GetRequirementResponsesQuery(userId, id), cancellationToken);
        return Ok(ApiResponseFactory.Success(responses, "Requirement responses."));
    }

    /// <summary>
    /// Owner only. ACCEPTED hires that professional: the requirement moves to IN_PROGRESS and all other pending
    /// responses are rejected. REJECTED declines one response and keeps the requirement open.
    /// </summary>
    [HttpPut("{id:guid}/responses/{responseId:guid}")]
    public async Task<ActionResult<RequirementResponseDto>> Decide(Guid id, Guid responseId, DecideResponseRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        if (!await IsCustomerAsync(userId, cancellationToken)) return Forbid();
        var response = await mediator.Send(new DecideResponseCommand(userId, id, responseId, request.Status), cancellationToken);
        return Ok(ApiResponseFactory.Success(response, "Response updated."));
    }

    private Task<bool> IsCustomerAsync(Guid userId, CancellationToken cancellationToken) =>
        profiles.HasTypeAsync(userId, "CUSTOMER", cancellationToken);
}
