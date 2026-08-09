using MediatR;
using PostService.Application.Features.Posts.DTOs;

namespace PostService.Application.Features.Posts.Commands.UpdatePost
{
    public sealed record UpdatePostCommand(
    Guid PostId,
    string Content,
    string ImageUrl,
    string Visibility) : IRequest<PostDto>;
}
