using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace PostService.Domain.Entity
{
    public class CommentLike
    {
        public Guid CommentLikeId { get; set; }
        public Guid UserId { get; set; }
        public Guid CommentId { get; set; }
        public DateTime CreatedAt { get; set; }

        //Navigation proerty
        public Comment Comments { get; set; }
    }
}

//Id
//CommentId
//UserId
//CreatedAt


