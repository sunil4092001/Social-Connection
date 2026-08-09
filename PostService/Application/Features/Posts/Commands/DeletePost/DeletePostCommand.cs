using MediatR;

namespace PostService.Application.Features.Posts.Commands.DeletePost
{
    public sealed record DeletePostCommand(Guid PostId) : IRequest<bool>;
   
}
