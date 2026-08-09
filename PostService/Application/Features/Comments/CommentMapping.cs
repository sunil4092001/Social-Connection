using PostService.Application.Features.Comments.Dtos;
using PostService.Application.Features.Posts.DTOs;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Comments
{
    public static class CommentMapping
    {
        public static CommentDto ToCommentDto(this Comment comment, string userName = "") =>
                        new(
                            comment.CommentId,
                            comment.PostId,
                            comment.UserId, 
                            userName,
                            comment.Content,
                            comment.CreatedAt
                            ); 
       
    }
}
