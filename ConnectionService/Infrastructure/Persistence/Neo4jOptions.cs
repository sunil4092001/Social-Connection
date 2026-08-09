namespace ConnectionService.Infrastructure.Persistence;

public sealed class Neo4jOptions
{
    public string Uri { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
