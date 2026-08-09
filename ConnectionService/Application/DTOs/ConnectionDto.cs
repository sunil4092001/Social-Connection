namespace ConnectionService.Application.DTOs
{
    public sealed record ConnectionDto(
        //Guid UserId, 
        //Guid ConnectedUserId, 
        //string Status, 
        //DateTime CreatedAtUtc


        Guid RequesterUserId,
        Guid AddresseeUserId,
        string Status,
        DateTime RequestedAtUtc,
        DateTime? ConnectedAtUtc
        );
    
    
}
