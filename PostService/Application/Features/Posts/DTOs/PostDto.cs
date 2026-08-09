namespace PostService.Application.Features.Posts.DTOs
{
    public sealed record PostDto(Guid Id, Guid AuthorUserId, string Content, DateTime CreatedAt);
    
        //public PostDto(Guid postId, Guid authorUserId, string content, DateTime createdAt)
        //{
        //    PostId = postId;
        //    AuthorUserId = authorUserId;
        //    Content = content;
        //    CreatedAt = createdAt;
        //}

        //public sealed record PostDto(Guid Id, Guid AuthorUserId, string Content, DateTime CreatedAtUtc);
        //public Guid PostId { get; set; }
        //public Guid AuthorUserId { get; set; }
        //public string Content { get; set; } = string.Empty;
        //public DateTime CreatedAt { get; set; }

        //public string ImageUrl { get; set; } = string.Empty;
        //public DateTime UpdatedAt { get; set; }
        //public string? Visibility { get; set; }
    
}
