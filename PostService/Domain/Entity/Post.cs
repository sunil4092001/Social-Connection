using Microsoft.AspNetCore.Http.HttpResults;

namespace PostService.Domain.Entity;

public  class Post
{
    public Guid PostId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Visibility { get; set; } = "Public";
    public bool IsDeleted { get; set; } 

    //Navigation
    public ICollection<Comment>? Comments { get; set; } = new List<Comment>();
    public ICollection<PostLike>? PostLikes { get; set; } = new List<PostLike>();

}

//Id(Guid)
//UserId(Guid)
//Content
//ImageUrl
//CreatedAt
//UpdatedAt
//Visibility
//IsDeleted