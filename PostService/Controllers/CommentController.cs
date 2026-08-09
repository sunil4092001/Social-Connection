using System.Security.Claims;
using BuildingBlocks.Response;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PostService.Application.Features.Comments.Commands;
using PostService.Application.Features.Comments.Commands.CreateCommand;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Comments.Request;
using PostService.Application.Features.Posts.Commands.CreatePost;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Features.Posts.Queries;
using PostService.Application.Features.Posts.Request;

namespace PostService.Controllers
{

    [ApiController]
    [Route("api/comments")]
    public sealed class CommentController(IMediator mediator) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CommentDto>>> CreateComment(CreateCommentRequest request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //Guid authorUserId = Guid.Parse(userId!);
            var comment = await mediator.Send(
                        new CreateCommentCommand(request.PostId, Guid.Parse(userId!), request.Content), 
                        cancellationToken);

            return Ok(ApiResponseFactory.Success(
                       comment,
                       "Comment Created Successfully"
                       )
                   );          
        }

        //[HttpGet("feed")]
        //public async Task<ActionResult<IReadOnlyList<ApiResponse<FeedPostDto>>>> GetFeed(
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    CancellationToken cancellationToken = default)
        //{
        //    var posts = await mediator.Send(new GetFeedQuery(page, pageSize), cancellationToken);
        //    // return Ok(posts);


        //    return Ok(ApiResponseFactory.Success(
        //                posts,
        //                "Post List fetched Successfully."
        //                ));
        //}
        //
    }
}
