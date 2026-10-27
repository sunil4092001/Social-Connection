using System.ComponentModel.DataAnnotations;

namespace IdentityService.Entities;

/// <summary>
/// A refresh token issued at login or refresh. Only a SHA-256 hash of the token is stored, so a database leak does
/// not expose usable tokens. Tokens are single use: each refresh revokes the old one and issues a new one.
/// </summary>
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Base64 SHA-256 hash of the token the client holds.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one on refresh (null when revoked by logout or reuse detection).</summary>
    public string? ReplacedByToken { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive(DateTime now) => !IsRevoked && ExpiryDate > now;
}
