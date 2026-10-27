using BuildingBlocks.Events;
using RequirementService.Domain;

namespace RequirementService.Application;

public sealed class RequirementNotificationOptions
{
    public const string SectionName = "RequirementNotification";
    public int MinimumMatchScore { get; init; } = 70;
    public int MaximumRecipients { get; init; } = 5;
    /// <summary>
    /// Minimum score for the professional "Recommended" list. Every same-trade requirement starts at 40 and
    /// availability + experience add at most 15, so 60 means at least some skill, location or network overlap.
    /// </summary>
    public decimal MinimumRecommendationScore { get; init; } = RequirementMatchingService.DefaultMinimumRecommendationScore;
}

public sealed record ProfessionalMatchProfile(
    Guid Id,
    string ProfessionalType,
    string? Location,
    int? ExperienceYears,
    string? AvailabilityStatus,
    IReadOnlyList<Guid> SkillIds,
    string UserType = "PROFESSIONAL");

/// <param name="NetworkDegree">1 or 2 when the requirement's customer is in the professional's network, otherwise null.</param>
public sealed record RequirementMatch(Requirement Requirement, decimal MatchScore, int? NetworkDegree = null);

public interface IRequirementMatchingService
{
    /// <param name="customerDegrees">Network degree from the professional to each customer (from ConnectionService).</param>
    IReadOnlyList<RequirementMatch> FindRecommendedRequirements(
        ProfessionalMatchProfile professional,
        IEnumerable<Requirement> requirements,
        decimal minimumScore = RequirementMatchingService.DefaultMinimumRecommendationScore,
        IReadOnlyDictionary<Guid, int>? customerDegrees = null);

    decimal CalculateScore(Requirement requirement, ProfessionalMatchProfile professional, int? networkDegree = null);

    /// <param name="professionalDegrees">Network degree from the customer to each professional (from ConnectionService).</param>
    IReadOnlyList<MatchingProfessional> FindMatches(
        Requirement requirement,
        IEnumerable<ProfessionalMatchProfile> professionals,
        RequirementNotificationOptions options,
        IReadOnlyDictionary<Guid, int>? professionalDegrees = null);
}

/// <summary>
/// Scores how well a professional fits a requirement (0-100):
///   40 same trade (required) + 30 skills + 15 location + 10 availability + 5 experience
///   + network trust bonus: 10 when customer and professional are connected, 5 when they share a connection.
/// The total is capped at 100, so the bonus decides between otherwise similar professionals.
/// </summary>
public sealed class RequirementMatchingService : IRequirementMatchingService
{
    public const decimal DefaultMinimumRecommendationScore = 60m;
    public const decimal FirstDegreeBonus = 10m;
    public const decimal SecondDegreeBonus = 5m;
    private const decimal MaxScore = 100m;

    public IReadOnlyList<RequirementMatch> FindRecommendedRequirements(
        ProfessionalMatchProfile professional,
        IEnumerable<Requirement> requirements,
        decimal minimumScore = DefaultMinimumRecommendationScore,
        IReadOnlyDictionary<Guid, int>? customerDegrees = null) => requirements
        .Where(requirement => requirement.Status == RequirementStatus.OPEN)
        .Where(requirement => string.Equals(requirement.ProfessionalType, professional.ProfessionalType, StringComparison.OrdinalIgnoreCase))
        .Select(requirement =>
        {
            int? degree = customerDegrees is not null && customerDegrees.TryGetValue(requirement.CustomerId, out var d) ? d : null;
            return new RequirementMatch(requirement, CalculateScore(requirement, professional, degree), degree);
        })
        .Where(match => match.MatchScore >= minimumScore)
        .OrderByDescending(match => match.MatchScore)
        .ThenByDescending(match => match.Requirement.CreatedAtUtc)
        .ToArray();

    public decimal CalculateScore(Requirement requirement, ProfessionalMatchProfile professional, int? networkDegree = null)
    {
        var requiredSkills = requirement.RequiredSkills.Select(skill => skill.SkillId).ToHashSet();
        var matchingSkillCount = requiredSkills.Count(professional.SkillIds.Contains);
        var skillsScore = requiredSkills.Count == 0
            ? 30m
            : 30m * matchingSkillCount / requiredSkills.Count;
        var locationScore = GetLocationScore(requirement.Location, professional.Location);
        var availabilityScore = professional.AvailabilityStatus?.ToUpperInvariant() switch
        {
            "AVAILABLE" => 10m,
            "BUSY" => 5m,
            _ => 0m
        };
        var experienceScore = professional.ExperienceYears.GetValueOrDefault() > 0 ? 5m : 0m;

        return Math.Min(MaxScore, 40m + skillsScore + locationScore + availabilityScore + experienceScore + NetworkBonus(networkDegree));
    }

    public IReadOnlyList<MatchingProfessional> FindMatches(
        Requirement requirement,
        IEnumerable<ProfessionalMatchProfile> professionals,
        RequirementNotificationOptions options,
        IReadOnlyDictionary<Guid, int>? professionalDegrees = null) => professionals
        .Where(professional => string.Equals(
            professional.ProfessionalType,
            requirement.ProfessionalType,
            StringComparison.OrdinalIgnoreCase))
        .Select(professional =>
        {
            int? degree = professionalDegrees is not null && professionalDegrees.TryGetValue(professional.Id, out var d) ? d : null;
            return new MatchingProfessional(
                professional.Id,
                (int)Math.Round(CalculateScore(requirement, professional, degree), MidpointRounding.AwayFromZero));
        })
        .Where(match => match.MatchScore >= options.MinimumMatchScore)
        .OrderByDescending(match => match.MatchScore)
        .ThenBy(match => match.ProfessionalId)
        .Take(Math.Max(0, options.MaximumRecipients))
        .ToArray();

    public static decimal NetworkBonus(int? degree) => degree switch
    {
        1 => FirstDegreeBonus,
        2 => SecondDegreeBonus,
        _ => 0m
    };

    private static decimal GetLocationScore(string requirementLocation, string? professionalLocation)
    {
        if (string.IsNullOrWhiteSpace(professionalLocation)) return 0;

        if (string.Equals(requirementLocation.Trim(), professionalLocation.Trim(), StringComparison.OrdinalIgnoreCase)) return 15m;

        return requirementLocation.Contains(professionalLocation, StringComparison.OrdinalIgnoreCase)
            || professionalLocation.Contains(requirementLocation, StringComparison.OrdinalIgnoreCase)
            ? 8m
            : 0m;
    }
}
