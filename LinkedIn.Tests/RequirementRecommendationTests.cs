using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RequirementService.Application;
using RequirementService.Controllers;
using RequirementService.Domain;
using RequirementService.Infrastructure;

namespace LinkedIn.Tests;

public sealed class RequirementRecommendationTests
{
    private readonly RequirementMatchingService matching = new();

    [Fact]
    public void Professional_type_is_a_hard_filter()
    {
        var requirement = CreateRequirement("MASON");
        var mason = CreateProfessional("MASON");
        var electrician = CreateProfessional("ELECTRICIAN");

        var matches = matching.FindRecommendedRequirements(mason, [requirement]);

        Assert.Single(matches);
        Assert.Equal(requirement.Id, matches[0].Requirement.Id);
    }

    [Fact]
    public void Different_professional_type_is_excluded()
    {
        var matches = matching.FindRecommendedRequirements(
            CreateProfessional("CARPENTER"),
            [CreateRequirement("MASON")]);

        Assert.Empty(matches);
    }

    [Fact]
    public void Full_skill_location_availability_and_experience_match_scores_100()
    {
        var skills = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var requirement = CreateRequirement("MASON", skills, location: "Faridabad");
        var professional = CreateProfessional("MASON", skills, location: "Faridabad", experienceYears: 8, availability: "AVAILABLE");

        var score = matching.CalculateScore(requirement, professional);

        Assert.Equal(100m, score);
    }

    [Fact]
    public void Partial_skill_match_preserves_fractional_score()
    {
        var skills = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var requirement = CreateRequirement("MASON", skills, location: "Faridabad");
        var professional = CreateProfessional("MASON", skills.Take(3), location: "Faridabad", experienceYears: 5, availability: "AVAILABLE");

        Assert.Equal(92.5m, matching.CalculateScore(requirement, professional));
    }

    [Fact]
    public void Exact_location_adds_fifteen_points()
    {
        var requirement = CreateRequirement("MASON", location: "Faridabad");
        var exact = CreateProfessional("MASON", location: "Faridabad", availability: "NOT_AVAILABLE");
        var other = CreateProfessional("MASON", location: "Delhi", availability: "NOT_AVAILABLE");

        Assert.Equal(15m, matching.CalculateScore(requirement, exact) - matching.CalculateScore(requirement, other));
    }

    [Fact]
    public void Available_status_contributes_ten_points_more_than_not_available()
    {
        var requirement = CreateRequirement("MASON", location: "Faridabad");
        var available = CreateProfessional("MASON", location: "Faridabad", experienceYears: 5, availability: "AVAILABLE");
        var unavailable = CreateProfessional("MASON", location: "Faridabad", experienceYears: 5, availability: "NOT_AVAILABLE");

        Assert.Equal(10m, matching.CalculateScore(requirement, available) - matching.CalculateScore(requirement, unavailable));
    }

    [Fact]
    public void Experience_contributes_five_points()
    {
        var requirement = CreateRequirement("MASON", location: "Faridabad");
        var experienced = CreateProfessional("MASON", location: "Faridabad", experienceYears: 5, availability: "BUSY");
        var noExperience = CreateProfessional("MASON", location: "Faridabad", experienceYears: 0, availability: "BUSY");

        Assert.Equal(5m, matching.CalculateScore(requirement, experienced) - matching.CalculateScore(requirement, noExperience));
    }

    [Fact]
    public void Only_open_requirements_are_returned()
    {
        var professional = CreateProfessional("MASON");
        var open = CreateRequirement("MASON");
        var closed = CreateRequirement("MASON", status: RequirementStatus.COMPLETED);

        var matches = matching.FindRecommendedRequirements(professional, [closed, open]);

        Assert.Single(matches);
        Assert.Equal(open.Id, matches[0].Requirement.Id);
    }

    [Fact]
    public void Recommendations_are_ordered_by_score_then_created_date()
    {
        var professional = CreateProfessional("MASON", location: "Faridabad", availability: "AVAILABLE");
        var olderExact = CreateRequirement("MASON", location: "Faridabad", createdAt: DateTime.UtcNow.AddDays(-2));
        var newerExact = CreateRequirement("MASON", location: "Faridabad", createdAt: DateTime.UtcNow);
        var partialLocation = CreateRequirement("MASON", location: "Faridabad Sector 1", createdAt: DateTime.UtcNow.AddDays(1));

        var matches = matching.FindRecommendedRequirements(professional, [partialLocation, olderExact, newerExact]);

        Assert.Equal(newerExact.Id, matches[0].Requirement.Id);
        Assert.Equal(olderExact.Id, matches[1].Requirement.Id);
        Assert.Equal(partialLocation.Id, matches[2].Requirement.Id);
    }

    [Fact]
    public async Task Non_professional_receives_forbidden()
    {
        var customer = CreateProfessional("MASON", userType: "CUSTOMER");
        var controller = CreateController(customer);

        var result = await controller.Recommended(CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task Missing_professional_profile_returns_not_found()
    {
        var controller = CreateController(null);

        var result = await controller.Recommended(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static Requirement CreateRequirement(
        string professionalType,
        IEnumerable<Guid>? requiredSkills = null,
        string location = "FBD",
        RequirementStatus status = RequirementStatus.OPEN,
        DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        Title = "Construction work",
        ProfessionalType = professionalType,
        Location = location,
        Status = status,
        CreatedAtUtc = createdAt ?? DateTime.UtcNow,
        RequiredSkills = (requiredSkills ?? []).Select(skillId => new RequirementSkill { SkillId = skillId }).ToList()
    };

    private static ProfessionalMatchProfile CreateProfessional(
        string type,
        IEnumerable<Guid>? skills = null,
        string? location = "FBD",
        int? experienceYears = 1,
        string? availability = "AVAILABLE",
        string userType = "PROFESSIONAL") => new(
            Guid.NewGuid(), type, location, experienceYears, availability, (skills ?? []).ToArray(), userType);

    private static RequirementsController CreateController(ProfessionalMatchProfile? profile)
    {
        var controller = new RequirementsController(null!, new FakeUserProfileClient(profile));
        var userId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "test"))
            }
        };
        return controller;
    }

    private sealed class FakeUserProfileClient(ProfessionalMatchProfile? profile) : IUserProfileClient
    {
        public Task<bool> HasTypeAsync(Guid userId, string expectedType, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ProfessionalMatchProfile?> GetProfessionalMatchProfileAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task<IReadOnlyList<ProfessionalMatchProfile>> SearchProfessionalsAsync(string professionalType, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProfessionalMatchProfile>>([]);
        public Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}