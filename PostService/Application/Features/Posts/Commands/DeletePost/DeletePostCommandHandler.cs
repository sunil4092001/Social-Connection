using MediatR;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Commands.DeletePost
{
    public sealed class DeletePostCommandHandler : IRequestHandler<DeletePostCommand, bool>
    {
        private readonly IPostRepository repository;

        public DeletePostCommandHandler(IPostRepository repository)
        {
            this.repository = repository; 
        }
        public async Task<bool> Handle(DeletePostCommand request, CancellationToken cancellationToken)
        {
            var psot  = await repository.GetPostByIdAsync(request.PostId, cancellationToken);

            if (psot == null)
                return false; ;

            await repository.DeleteAsync(psot, cancellationToken);

            return true;
        }
    }
}
