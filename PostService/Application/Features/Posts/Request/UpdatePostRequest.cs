namespace PostService.Application.Features.Posts.Request;

/// <summary>ImageUrl and Visibility are optional; when omitted the post keeps its current values.</summary>
public sealed record UpdatePostRequest(
    string Content,
    string? ImageUrl,
    string? Visibility);
