namespace ConnectionService.Application.DTOs
{
    public sealed record SentRequestDto 
    (
        Guid UserId,
        string FirstName,
        string LastName,
        //string? ProfileImageUrl,
        DateTime RequestedAtUtc
    );
}
