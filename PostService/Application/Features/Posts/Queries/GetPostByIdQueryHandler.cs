using MediatR;
using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Posts.DTOs;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Queries
{
    public class GetPostByIdQueryHandler : IRequestHandler<GetPostByIdQuery, PostSummaryDto>
    {
        //public readonly IPostRepository _postRepository ;
        private readonly IPostRepository _repository;
        public GetPostByIdQueryHandler(IPostRepository repository)
        {
            _repository = repository;
        }
        public async Task<PostSummaryDto?> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
        {

            var post = await _repository.GetPostByIdAsync(request.PostId, cancellationToken);

            if (post == null)
                return null;


            return new PostSummaryDto(
                post.PostId,
                post.AuthorUserId,
                post.Content,
                post.ImageUrl,
                post.CreatedAt,
                post.Visibility
                //post.CommentCount = ,

                );


            // user for full post detail

            //var post = await _repository.GetPostByIdAsync(request.PostId, cancellationToken);

            //if (post == null)
            //    return null;

            //return new PostDetailsDto(
            //    post.PostId,
            //    post.AuthorUserId,
            //    post.Content,
            //    post.ImageUrl,
            //    post.CreatedAt,
            //    post.Visibility,

            //    post.Comments.Select(c =>
            //        new CommentDto(
            //            c.CommentId,
            //            c.UserId,
            //            c.Content,
            //            c.CreatedAt,

            //            c.CommentLikes.Select(l =>
            //                new CommentLikeDto(
            //                    l.CommentLikeId,
            //                    l.UserId,
            //                    l.CreatedAt))
            //            .ToList()))
            //    .ToList(),

            //    post.PostLikes.Select(l =>
            //        new PostLikeDto(
            //            l.PostLikeId,
            //            l.UserId,
            //            l.CreatedAt))
            //    .ToList());

        }
    }
}
