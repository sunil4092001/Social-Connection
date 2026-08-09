using System.Security.Claims;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostService.Application;
using PostService.Application.Common;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Comments.Queries;
using PostService.Application.Features.Posts.Commands.CreatePost;
using PostService.Application.Features.Posts.Commands.DeletePost;
using PostService.Application.Features.Posts.Commands.UpdatePost;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Features.Posts.Queries;
using PostService.Application.Features.Posts.Request;

namespace PostService.Controllers;

[ApiController]
[Route("api/posts")]
public sealed class PostsController(IMediator mediator) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PostDto>>> Create(CreatePostRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //Guid authorUserId = Guid.Parse(userId!);
        var post = await mediator.Send(new CreatePostCommand(Guid.Parse(userId!), request.Content), cancellationToken);
        //return CreatedAtAction(nameof(GetFeed), new { page = 1, pageSize = 20 }, post);
        return CreatedAtAction(nameof(GetFeed), new { page = 1, pageSize = 20 }, ApiResponseFactory.Success(
                    post,
                    "Post Create Successfully."
                    ));
        

    }

    [HttpGet("feed")]
    //public async Task<ActionResult<IReadOnlyList<ApiResponse<PostDto>>>> GetFeed(
    public async Task<ActionResult<IReadOnlyList<ApiResponse<FeedPostDto>>>> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var posts = await mediator.Send(new GetFeedQuery(page, pageSize), cancellationToken);
        // return Ok(posts);
      

        return Ok(ApiResponseFactory.Success(
                    posts,
                    "Post List fetched Successfully."
                    ));
    }
    //

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PostDto>>> GetPostById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var post = await mediator.Send(
            new GetPostByIdQuery(id),
            cancellationToken);

        if (post is null)
            return NotFound(ApiResponseFactory.Success(
                    post,
                    "Post does not Exist"
                    ));

        //return Ok(post);
        return Ok(ApiResponseFactory.Success(
                    post,
                    "Post fetch successfully."
                    ));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PostDto>>> Update(
        Guid id,
        UpdatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await mediator.Send(
            new UpdatePostCommand(
                id,
                request.Content,
                request.ImageUrl,
                request.Visibility),
            cancellationToken);

        if (post is null)
            return NotFound();

        return Ok(ApiResponseFactory.Success(
                    post,
                    "Post Updated Successfully."
                    ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await mediator.Send(
            new DeletePostCommand(id),
            cancellationToken);

        if (!deleted)
            return NotFound(ApiResponseFactory.Success(
                        false,
                        "Post Not deleted successfully."
                        ));

        return Ok(ApiResponseFactory.Success(
                    true,
                    "Post deleted successfully."
                    ));
    }



    #region ======================[Comment]======================

    [HttpGet("{postId:guid}/comments")]
    public async Task<IActionResult> GetCommetsByPost(Guid postId, CancellationToken cancellationToken = default)
    {
        var comments = await mediator.Send(
                new GetPostCommentQuery(postId),
                cancellationToken);
         

        if (comments == null)
            return NotFound(ApiResponseFactory.Failure<CommentDto>(                        
                        "Comments not found.",
                        null
                        ));

        return Ok(ApiResponseFactory.Success <IReadOnlyList<CommentDto>>(
                    comments,
                    "Comment fetched successfully."
                    ));
    }
    

    #endregion
}
