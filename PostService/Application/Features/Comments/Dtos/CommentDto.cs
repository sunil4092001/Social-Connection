namespace PostService.Application.Features.Comments.Dtos;

/// <summary>
/// A comment from the viewer's point of view. CanEdit: the viewer wrote it. CanDelete: the viewer wrote it or owns the
/// post. The UI uses these flags instead of guessing identities.
/// </summary>
public sealed record CommentDto(
    Guid CommentId,
    Guid PostId,
    Guid UserId,
    string UserName,
    string Content,
    DateTime CreatedAt,
    bool IsEdited,
    bool CanEdit,
    bool CanDelete);
