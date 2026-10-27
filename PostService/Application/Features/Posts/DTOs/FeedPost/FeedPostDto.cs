namespace PostService.Application.Features.Posts.DTOs.FeedPost;

/// <summary>A post as shown in the feed, on a profile or on its own page, from the viewer's point of view.</summary>
public sealed record FeedPostDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorName,
    string Content,
    string? ImageUrl,
    DateTime CreatedAt,
    bool IsEdited,
    int LikeCount,
    int CommentCount,
    bool LikedByMe,
    bool IsMine);

/// <summary>Result of liking or unliking: the new state, so the UI can update without reloading.</summary>
public sealed record PostLikeStateDto(Guid PostId, int LikeCount, bool LikedByMe);
