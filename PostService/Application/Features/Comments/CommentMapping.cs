using PostService.Application.Features.Comments.Dtos;
using PostService.Domain.Entity;

namespace PostService.Application.Features.Comments;

public static class CommentMapping
{
    public static CommentDto ToCommentDto(this Comment comment, string userName, Guid viewerId, Guid postAuthorId) =>
        new(
            comment.CommentId,
            comment.PostId,
            comment.UserId,
            userName,
            comment.Content,
            comment.CreatedAt,
            IsEdited: comment.UpdatedAt > comment.CreatedAt, // creation sets both to the same instant
            CanEdit: comment.UserId == viewerId,
            CanDelete: comment.UserId == viewerId || postAuthorId == viewerId);
}
