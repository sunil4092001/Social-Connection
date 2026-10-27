using ConnectionService.Application.DTOs;
using ConnectionService.Application.Interfaces;

namespace ConnectionService.Application;

internal static class UserNames
{
    public const string Fallback = "BuildConnect member";

    /// <summary>Adds display names from UserService to graph results. Unknown users get a neutral fallback name.</summary>
    public static async Task<IReadOnlyList<ConnectionUserDto>> AttachAsync(IUserDirectoryClient directory, IReadOnlyList<ConnectionEdge> edges, CancellationToken cancellationToken)
    {
        if (edges.Count == 0) return [];
        var names = await directory.GetDisplayNamesAsync(edges.Select(edge => edge.UserId), cancellationToken);
        return edges.Select(edge => new ConnectionUserDto(edge.UserId, names.GetValueOrDefault(edge.UserId, Fallback), edge.SinceUtc)).ToList();
    }
}
