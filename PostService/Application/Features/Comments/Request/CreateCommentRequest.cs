namespace PostService.Application.Features.Comments.Request
{
    public sealed record CreateCommentRequest
    (
        Guid PostId,
        string Content
    );
}
