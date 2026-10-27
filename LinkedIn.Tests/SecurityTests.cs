using BuildingBlocks.Authentication;
using BuildingBlocks.Exceptions;
using IdentityService.DTOs;
using IdentityService.Entities;
using IdentityService.Repository;
using IdentityService.Services;
using LinkedIn.Tests.Fakes;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PostService.Application.Features.Comments.Commands.CreateCommand;
using PostService.Application.Features.Posts.Commands.DeletePost;
using PostService.Application.Features.Posts.Commands.UpdatePost;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace LinkedIn.Tests;

/// <summary>Session rules (refresh-token rotation and theft detection), startup key check and post ownership.</summary>
public sealed class SecurityTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = new string('k', 64),
            ["Jwt:Issuer"] = "IdentityService",
            ["Jwt:Audience"] = "IdentityServiceUsers"
        })
        .Build();

    // ----- Refresh tokens -----

    [Fact]
    public async Task Login_stores_only_a_hash_of_the_refresh_token()
    {
        var (auth, repository) = CreateAuth();

        var tokens = await auth.Login(new LoginRequest { Email = "Amit@Example.com", Password = "SecurePass123" });

        Assert.NotNull(tokens);
        var stored = Assert.Single(repository.Tokens);
        Assert.NotEqual(tokens.RefreshToken, stored.Token);
        Assert.False(stored.IsRevoked);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var (auth, _) = CreateAuth();

        Assert.Null(await auth.Login(new LoginRequest { Email = "amit@example.com", Password = "wrong" }));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_stops_working()
    {
        var (auth, repository) = CreateAuth();
        var first = (await auth.Login(new LoginRequest { Email = "amit@example.com", Password = "SecurePass123" }))!;

        var second = await auth.RefreshToken(first.RefreshToken);

        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(2, repository.Tokens.Count);
        Assert.True(repository.Tokens[0].IsRevoked);
        Assert.Equal(repository.Tokens[1].Token, repository.Tokens[0].ReplacedByToken);
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_ends_every_session_of_the_user()
    {
        var (auth, repository) = CreateAuth();
        var first = (await auth.Login(new LoginRequest { Email = "amit@example.com", Password = "SecurePass123" }))!;
        var second = (await auth.RefreshToken(first.RefreshToken))!;

        // Someone replays the first token (for example a stolen copy).
        Assert.Null(await auth.RefreshToken(first.RefreshToken));

        Assert.All(repository.Tokens, token => Assert.True(token.IsRevoked));
        Assert.Null(await auth.RefreshToken(second.RefreshToken));
    }

    [Fact]
    public async Task Expired_or_unknown_refresh_tokens_are_rejected()
    {
        var (auth, repository) = CreateAuth();
        var tokens = (await auth.Login(new LoginRequest { Email = "amit@example.com", Password = "SecurePass123" }))!;
        repository.Tokens[0].ExpiryDate = DateTime.UtcNow.AddMinutes(-1);

        Assert.Null(await auth.RefreshToken(tokens.RefreshToken));
        Assert.Null(await auth.RefreshToken("not-a-real-token"));
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (auth, _) = CreateAuth();
        var tokens = (await auth.Login(new LoginRequest { Email = "amit@example.com", Password = "SecurePass123" }))!;

        await auth.Logout(tokens.RefreshToken);

        Assert.Null(await auth.RefreshToken(tokens.RefreshToken));
    }

    [Fact]
    public void Services_refuse_to_start_without_a_strong_jwt_key()
    {
        var weak = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "short" }).Build();

        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuthentication(weak));
        Assert.Contains("setup-dev-secrets", exception.Message);
    }

    // ----- Post ownership -----

    [Fact]
    public async Task Only_the_author_can_edit_or_delete_a_post()
    {
        var author = Guid.NewGuid();
        var repository = new InMemoryPostRepository();
        var post = repository.Seed(author, "Original");
        var stranger = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() => new UpdatePostCommandHandler(repository)
            .Handle(new UpdatePostCommand(post.PostId, stranger, "Hacked", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => new DeletePostCommandHandler(repository)
            .Handle(new DeletePostCommand(post.PostId, stranger), CancellationToken.None));
        Assert.Equal("Original", post.Content);
        Assert.False(post.IsDeleted);

        await new UpdatePostCommandHandler(repository).Handle(new UpdatePostCommand(post.PostId, author, "Edited", null, null), CancellationToken.None);
        Assert.Equal("Edited", post.Content);
    }

    [Fact]
    public async Task Comment_on_a_missing_post_is_not_found()
    {
        var repository = new InMemoryPostRepository();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CreateCommentCommandHandler(repository, new NoUsers(), new NoOutbox())
            .Handle(new CreateCommentCommand(Guid.NewGuid(), Guid.NewGuid(), "Nice work"), CancellationToken.None));
    }

    [Fact]
    public void Empty_comment_is_invalid() =>
        Assert.False(new CreateCommentValidator().Validate(new CreateCommentCommand(Guid.NewGuid(), Guid.NewGuid(), "")).IsValid);

    // ----- Helpers -----

    private static (AuthService, InMemoryAuthRepository) CreateAuth()
    {
        var repository = new InMemoryAuthRepository();
        repository.Users.Add(new User { Id = Guid.NewGuid(), Email = "amit@example.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123"), IsActive = true });
        return (new AuthService(repository, new JwtService(Configuration), Configuration, NullLogger<AuthService>.Instance), repository);
    }

    private sealed class InMemoryAuthRepository : IAuthRepository
    {
        public List<User> Users { get; } = [];
        public List<RefreshToken> Tokens { get; } = [];

        public Task RegisterAsync(User request) { Users.Add(request); return Task.CompletedTask; }
        public Task DeleteUserAsync(Guid userId) { Users.RemoveAll(user => user.Id == userId); return Task.CompletedTask; }
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash)
        {
            var token = Tokens.FirstOrDefault(t => t.Token == tokenHash);
            if (token is not null) token.User = Users.Single(user => user.Id == token.UserId);
            return Task.FromResult(token);
        }
        public Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(Guid userId, DateTime now) =>
            Task.FromResult<IReadOnlyList<RefreshToken>>(Tokens.Where(t => t.UserId == userId && t.IsActive(now)).ToList());
        public Task AddRefreshTokenAsync(RefreshToken token) { Tokens.Add(token); return Task.CompletedTask; }
        public Task SaveChangesAsync() => Task.CompletedTask;
    }

    private sealed class NoUsers : IUserServiceClient
    {
        public Task<Dictionary<Guid, PostService.Application.Features.Posts.DTOs.FeedPost.UserSummaryDto>> GetUsersAsync(IEnumerable<Guid> autherIds, CancellationToken cancellationToken) => Task.FromResult(new Dictionary<Guid, PostService.Application.Features.Posts.DTOs.FeedPost.UserSummaryDto>());
    }

    private sealed class NoOutbox : IEventOutbox
    {
        public void Add<TEvent>(string topic, string key, TEvent @event) where TEvent : notnull { }
    }
}
