using RequirementService.Application;
using RequirementService.Domain;

namespace LinkedIn.Tests;

/// <summary>The network trust bonus: connections from Neo4j influence who is matched and what is recommended.</summary>
public sealed class NetworkMatchingTests
{
    private readonly RequirementMatchingService matching = new();

    [Theory]
    [InlineData(null, 0)]
    [InlineData(1, 10)]
    [InlineData(2, 5)]
    [InlineData(3, 0)]
    public void Bonus_depends_on_network_degree(int? degree, int expectedBonus) =>
        Assert.Equal(expectedBonus, RequirementMatchingService.NetworkBonus(degree));

    [Fact]
    public void Score_never_exceeds_100()
    {
        var requirement = Requirement("MASON", "Pune");
        var perfect = Professional("MASON", "Pune", "AVAILABLE", 10);

        Assert.Equal(100m, matching.CalculateScore(requirement, perfect));
        Assert.Equal(100m, matching.CalculateScore(requirement, perfect, networkDegree: 1));
    }

    [Fact]
    public void Connected_professional_outranks_an_otherwise_identical_stranger()
    {
        var requirement = Requirement("MASON", "Pune");
        var stranger = Professional("MASON", "Mumbai", "BUSY", 3);
        var connected = Professional("MASON", "Mumbai", "BUSY", 3);
        var options = new RequirementNotificationOptions { MinimumMatchScore = 0, MaximumRecipients = 1 };

        var matches = matching.FindMatches(requirement, [stranger, connected], options, new Dictionary<Guid, int> { [connected.Id] = 1 });

        var winner = Assert.Single(matches);
        Assert.Equal(connected.Id, winner.ProfessionalId);
        Assert.Equal(90, winner.MatchScore); // 40 + 30 skills + 0 location + 5 busy + 5 experience + 10 network
    }

    [Fact]
    public void Second_degree_bonus_can_lift_a_professional_over_the_notification_threshold()
    {
        var requirement = Requirement("MASON", "Pune");
        var professional = Professional("MASON", "Mumbai", "AVAILABLE", 0); // 40 + 30 + 0 + 10 + 0 = 80
        var options = new RequirementNotificationOptions { MinimumMatchScore = 85, MaximumRecipients = 5 };

        Assert.Empty(matching.FindMatches(requirement, [professional], options));
        Assert.Single(matching.FindMatches(requirement, [professional], options, new Dictionary<Guid, int> { [professional.Id] = 2 }));
    }

    [Fact]
    public void Recommendations_report_the_customer_network_degree_and_rank_network_jobs_first()
    {
        var professional = Professional("MASON", "Mumbai", "BUSY", 3);
        var fromStranger = Requirement("MASON", "Pune", createdAt: DateTime.UtcNow);
        var fromConnection = Requirement("MASON", "Pune", createdAt: DateTime.UtcNow.AddDays(-3));

        var recommended = matching.FindRecommendedRequirements(professional, [fromStranger, fromConnection], 0m,
            new Dictionary<Guid, int> { [fromConnection.CustomerId] = 1 });

        Assert.Equal(fromConnection.Id, recommended[0].Requirement.Id);
        Assert.Equal(1, recommended[0].NetworkDegree);
        Assert.Null(recommended[1].NetworkDegree);
    }

    private static Requirement Requirement(string trade, string location, DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        Title = "Wall",
        ProfessionalType = trade,
        Location = location,
        Status = RequirementStatus.OPEN,
        CreatedAtUtc = createdAt ?? DateTime.UtcNow
    };

    private static ProfessionalMatchProfile Professional(string trade, string location, string availability, int experience) =>
        new(Guid.NewGuid(), trade, location, experience, availability, []);
}
