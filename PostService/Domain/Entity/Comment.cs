namespace PostService.Domain.Entity
{
    public class Comment
    {
        public Guid CommentId { get; set; }
        public Guid PostId { get; set; } //FK
        public Guid UserId { get; set; }
        public string Content { get; set; } = string.Empty; 
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        //Navigation property

        public Post Posts { get; set; }
        public ICollection<CommentLike>? CommentLikes { get; set; } = new List<CommentLike>();

    }
}

//Id(Guid)
//PostId(Guid)
//UserId(Guid)
//Content
//CreatedAt
//UpdatedAt
