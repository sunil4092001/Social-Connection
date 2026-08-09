using Microsoft.AspNetCore.Http.HttpResults;

namespace PostService.Domain.Entity
{
    public class PostLike
    {
        public Guid PostLikeId { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        //Navigation property
        public Post? Posts { get; set; }
    }
}

//Id(Guid)
//PostId(Guid)
//UserId(Guid)
//CreatedAt
