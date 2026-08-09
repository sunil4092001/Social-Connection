using FluentValidation;
using MediatR;
using PostService.Application.Features.Posts.DTOs;

namespace PostService.Application.Features.Posts.Commands.CreatePost
{
    public sealed record CreatePostCommand(Guid AuthorUserId, string Content) : IRequest<PostDto>
    {
       
    }
}
