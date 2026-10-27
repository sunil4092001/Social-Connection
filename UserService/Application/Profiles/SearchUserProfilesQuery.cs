using MediatR;
using UserService.Application.Dtos;
using UserService.Domain;

namespace UserService.Application.Profiles;

public sealed record SearchUserProfilesQuery(
	string? Term,
	ProfessionalType? ProfessionalType,
	string? Location,
	string? Skill,
	int? MinimumExperienceYears,
	AvailabilityStatus? AvailabilityStatus) : IRequest<IReadOnlyList<Dtos.UpdateUserProfileCommand>>;
