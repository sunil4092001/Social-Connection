using BuildingBlocks.Common;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostService.Application.Features.Comments.Commands;
using PostService.Application.Features.Comments.Commands.CreateCommand;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Comments.Request;

namespace PostService.Controllers;

/// <summary>Comment author edits; comment author or post author deletes. The user always comes from the JWT.</summary>
[ApiController]
[Authorize]
[Route("api/comments")]
public sealed class CommentController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CommentDto>>> CreateComment(CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var comment = await mediator.Send(new CreateCommentCommand(request.PostId, userId, request.Content), cancellationToken);
        return Ok(ApiResponseFactory.Success(comment, "Comment created successfully."));
    }

    [HttpPut("{commentId:guid}")]
    public async Task<ActionResult<ApiResponse<CommentDto>>> UpdateComment(Guid commentId, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var comment = await mediator.Send(new UpdateCommentCommand(commentId, userId, request.Content), cancellationToken);
        return Ok(ApiResponseFactory.Success(comment, "Comment updated successfully."));
    }

    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await mediator.Send(new DeleteCommentCommand(commentId, userId), cancellationToken);
        return NoContent();
    }
}
