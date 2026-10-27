using System.Security.Cryptography;
using System.Text;

namespace BuildingBlocks.Events;

/// <summary>
/// Published by PostService when someone likes another user's post. Recipient: the post author.
/// EventId is derived from (post, liker), so liking, unliking and liking again yields at most one notification.
/// </summary>
public sealed record PostLikedEvent(
    Guid EventId,
    Guid PostId,
    Guid PostAuthorUserId,
    Guid LikerUserId,
    string LikerName,
    string PostExcerpt,
    DateTime OccurredAtUtc)
{
    public static Guid StableEventId(Guid postId, Guid likerUserId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"post-liked:{postId}:{likerUserId}")));
}

/// <summary>Published by PostService when someone comments on another user's post. Recipient: the post author.</summary>
public sealed record PostCommentedEvent(
    Guid EventId,
    Guid PostId,
    Guid CommentId,
    Guid PostAuthorUserId,
    Guid CommenterUserId,
    string CommenterName,
    string CommentExcerpt,
    DateTime OccurredAtUtc);
