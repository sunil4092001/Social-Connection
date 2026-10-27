using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts;

/// <summary>Turns database views into DTOs and adds author names from UserService (one batch call per page).</summary>
internal static class PostPresentation
{
    public const string UnknownUser = "BuildConnect member";

    public static async Task<IReadOnlyList<FeedPostDto>> ToDtosAsync(IReadOnlyList<PostView> views, Guid viewerId, IUserServiceClient users, CancellationToken cancellationToken)
    {
        if (views.Count == 0) return [];
        var authors = await users.GetUsersAsync(views.Select(view => view.AuthorUserId), cancellationToken);
        return views.Select(view => ToDto(view, viewerId, authors.TryGetValue(view.AuthorUserId, out var author) ? $"{author.FirstName} {author.LastName}".Trim() : UnknownUser)).ToList();
    }

    public static FeedPostDto ToDto(PostView view, Guid viewerId, string authorName) => new(
        view.PostId,
        view.AuthorUserId,
        authorName,
        view.Content,
        string.IsNullOrWhiteSpace(view.ImageUrl) ? null : view.ImageUrl,
        view.CreatedAt,
        IsEdited: view.UpdatedAt > view.CreatedAt, // creation sets both to the same instant
        view.LikeCount,
        view.CommentCount,
        view.LikedByViewer,
        IsMine: view.AuthorUserId == viewerId);

    public static async Task<string> NameOfAsync(IUserServiceClient users, Guid userId, CancellationToken cancellationToken)
    {
        var found = await users.GetUsersAsync([userId], cancellationToken);
        return found.TryGetValue(userId, out var user) ? $"{user.FirstName} {user.LastName}".Trim() : UnknownUser;
    }

    /// <summary>Short preview used in notification text.</summary>
    public static string Excerpt(string text, int length = 80)
    {
        var singleLine = text.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= length ? singleLine : singleLine[..(length - 1)] + "…";
    }
}
