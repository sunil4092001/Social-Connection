using BuildingBlocks.Common;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Comments.Queries;
using PostService.Application.Features.Likes;
using PostService.Application.Features.Posts.Commands.CreatePost;
using PostService.Application.Features.Posts.Commands.DeletePost;
using PostService.Application.Features.Posts.Commands.UpdatePost;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Features.Posts.Queries;
using PostService.Application.Features.Posts.Request;

namespace PostService.Controllers;

/// <summary>All post endpoints require a signed-in user; only the author can edit or delete a post.</summary>
[ApiController]
[Authorize]
[Route("api/posts")]
public sealed class PostsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PostDto>>> Create(CreatePostRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var post = await mediator.Send(new CreatePostCommand(userId, request.Content), cancellationToken);
        return CreatedAtAction(nameof(GetPostById), new { id = post.Id }, ApiResponseFactory.Success(post, "Post created successfully."));
    }

    /// <summary>scope=network (default): my posts and my connections' posts. scope=all: everyone's posts.</summary>
    [HttpGet("feed")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FeedPostDto>>>> GetFeed(
        [FromQuery] string scope = FeedScope.Network,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var posts = await mediator.Send(new GetFeedQuery(userId, scope.Trim().ToLowerInvariant(), page, pageSize), cancellationToken);
        return Ok(ApiResponseFactory.Success(posts, "Feed."));
    }

    /// <summary>Posts written by one user (profile page).</summary>
    [HttpGet("user/{authorUserId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FeedPostDto>>>> GetUserPosts(
        Guid authorUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var posts = await mediator.Send(new GetUserPostsQuery(userId, authorUserId, page, pageSize), cancellationToken);
        return Ok(ApiResponseFactory.Success(posts, "User posts."));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<FeedPostDto>>> GetPostById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var post = await mediator.Send(new GetPostByIdQuery(id, userId), cancellationToken);
        return post is null
            ? NotFound(ApiResponseFactory.Failure<FeedPostDto>("Post was not found."))
            : Ok(ApiResponseFactory.Success(post, "Post."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PostDto>>> Update(Guid id, UpdatePostRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var post = await mediator.Send(new UpdatePostCommand(id, userId, request.Content, request.ImageUrl, request.Visibility), cancellationToken);
        return post is null
            ? NotFound(ApiResponseFactory.Failure<PostDto>("Post was not found."))
            : Ok(ApiResponseFactory.Success(post, "Post updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var deleted = await mediator.Send(new DeletePostCommand(id, userId), cancellationToken);
        return deleted
            ? Ok(ApiResponseFactory.Success(true, "Post deleted successfully."))
            : NotFound(ApiResponseFactory.Failure<bool>("Post was not found."));
    }

    // ----- Likes (idempotent) -----

    [HttpPost("{id:guid}/like")]
    public async Task<ActionResult<ApiResponse<PostLikeStateDto>>> Like(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new LikePostCommand(id, userId), cancellationToken), "Post liked."));
    }

    [HttpDelete("{id:guid}/like")]
    public async Task<ActionResult<ApiResponse<PostLikeStateDto>>> Unlike(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return Ok(ApiResponseFactory.Success(await mediator.Send(new UnlikePostCommand(id, userId), cancellationToken), "Like removed."));
    }

    // ----- Comments of a post -----

    [HttpGet("{postId:guid}/comments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CommentDto>>>> GetComments(Guid postId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var comments = await mediator.Send(new GetPostCommentQuery(postId, userId), cancellationToken);
        return Ok(ApiResponseFactory.Success(comments, "Comments."));
    }
}
