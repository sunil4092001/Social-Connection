using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using BuildingBlocks.Response;
using RequirementService.Application;

namespace RequirementService.Infrastructure;

public interface IUserProfileClient
{
    Task<bool> HasTypeAsync(Guid userId, string expectedType, CancellationToken cancellationToken);
    Task<ProfessionalMatchProfile?> GetProfessionalMatchProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProfessionalMatchProfile>> SearchProfessionalsAsync(string professionalType, CancellationToken cancellationToken);
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>Looks up "First Last" names for many users in one call. Missing users are left out of the result.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}

public sealed class UserProfileClient(
    HttpClient httpClient,
    IHttpContextAccessor contextAccessor,
    ILogger<UserProfileClient> logger) : IUserProfileClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> HasTypeAsync(Guid userId, string expectedType, CancellationToken cancellationToken)
    {
        using var request = CreateRequest($"api/users/{userId}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("userType", out var userType)
            && string.Equals(userType.GetString(), expectedType, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProfessionalMatchProfile?> GetProfessionalMatchProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest($"api/users/{userId}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ProfessionalMatchProfileDto>>(JsonOptions, cancellationToken);
        var profile = result?.Data;
        if (profile is null) return null;

        return new ProfessionalMatchProfile(
            userId,
            profile.ProfessionalType ?? string.Empty,
            profile.Location,
            profile.ExperienceYears,
            profile.AvailabilityStatus,
            profile.Skills.Select(skill => skill.SkillId).ToArray(),
            profile.UserType);
    }

    public async Task<IReadOnlyList<ProfessionalMatchProfile>> SearchProfessionalsAsync(
        string professionalType,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest($"api/users/search?professionalType={Uri.EscapeDataString(professionalType.ToString())}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("UserService professional search returned {StatusCode}", response.StatusCode);
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<UserProfileSearchDto>>>(JsonOptions, cancellationToken);
        return (result?.Data ?? []).Select(profile => new ProfessionalMatchProfile(
            profile.Id,
            profile.ProfessionalType ?? string.Empty,
            profile.Location,
            profile.ExperienceYears,
            profile.AvailabilityStatus,
            profile.Skills.Select(skill => skill.SkillId).ToArray())).ToArray();
    }

    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest($"api/users/{userId}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileDetailDto>>(JsonOptions, cancellationToken);
        var profile = result?.Data;
        return profile is null
            ? null
            : string.IsNullOrWhiteSpace(profile.DisplayName)
                ? $"{profile.FirstName} {profile.LastName}".Trim()
                : profile.DisplayName;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        try
        {
            using var request = CreateRequest("api/users/batch", HttpMethod.Post);
            request.Content = JsonContent.Create(ids);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService batch lookup returned {StatusCode}", response.StatusCode);
                return new Dictionary<Guid, string>();
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>(JsonOptions, cancellationToken) ?? [];
            return users
                .GroupBy(user => user.Id)
                .ToDictionary(group => group.Key, group => $"{group.First().FirstName} {group.First().LastName}".Trim());
        }
        catch (HttpRequestException exception)
        {
            // Names are decoration; the workflow must keep working when UserService is unavailable.
            logger.LogWarning(exception, "UserService batch lookup failed");
            return new Dictionary<Guid, string>();
        }
    }

    private HttpRequestMessage CreateRequest(string relativeUri, HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, relativeUri);
        var token = contextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        return request;
    }

    private sealed record UserProfileSearchDto(
        Guid Id,
        string? DisplayName,
        string FirstName,
        string LastName,
        string? Location,
        string? ProfessionalType,
        int? ExperienceYears,
        string? AvailabilityStatus,
        IReadOnlyList<UserProfileSkillDto> Skills);

    private sealed record UserSummaryDto(Guid Id, string FirstName, string LastName);
    private sealed record UserProfileDetailDto(string DisplayName, string FirstName, string LastName);
    private sealed record ProfessionalMatchProfileDto(
        string UserType,
        string? ProfessionalType,
        string? Location,
        int? ExperienceYears,
        string? AvailabilityStatus,
        IReadOnlyList<UserProfileSkillDto> Skills);
    private sealed record UserProfileSkillDto(Guid SkillId, string Name, string Category, int ExperienceYears, string SkillLevel);
}
