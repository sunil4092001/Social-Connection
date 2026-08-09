using System.ComponentModel.DataAnnotations;

namespace IdentityService.Entities
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }

        public bool IsRevoked { get; set; }

        public User User { get; set; }
    }
}
