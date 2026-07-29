using Pettle.Domain.Common;

namespace Pettle.Domain.Identity;

/// <summary>One row per issued refresh token (one per login/device), so refreshing on one
/// device/tab never invalidates another still-active session for the same user.</summary>
public class RefreshTokenEntry : Entity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
