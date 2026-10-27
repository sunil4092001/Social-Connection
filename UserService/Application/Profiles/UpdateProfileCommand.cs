using MediatR;
using UserService.Application.Dtos;
using UserService.Domain;

namespace UserService.Application.Profiles;

public sealed record UpdateProfileCommand(
    Guid UserId, string FirstName, string LastName, string? DisplayName, string? Headline,
    string? About, string? Location, ProfessionalType? ProfessionalType, int? ExperienceYears,
    AvailabilityStatus? AvailabilityStatus, string? ProfileImageUrl) : IRequest<UpdateUserProfileCommand>;
