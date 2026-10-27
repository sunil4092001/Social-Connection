using System.Text.Json;
using BuildingBlocks.Events;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Messaging.Outbox;
using LinkedIn.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application;
using PostService.Application.Features.Comments.Commands;
using PostService.Application.Features.Comments.Commands.CreateCommand;
using PostService.Application.Features.Comments.Queries;
using PostService.Application.Features.Likes;
using PostService.Application.Features.Posts.Commands.DeletePost;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Features.Posts.Queries;
using PostService.Application.Interfaces;
using PostService.Domain.Entity;

namespace LinkedIn.Tests;

/// <summary>Likes, comments, soft delete, the network feed and the notifications post events produce.</summary>
public sealed class PostWorkflowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Guid author = Guid.NewGuid();
    private readonly Guid reader = Guid.NewGuid();
    private readonly InMemoryPostRepository repository = new();
    private readonly RecordingOutbox outbox = new();
    private readonly NamedUsers users = new();

    // ----- Likes -----

    [Fact]
    public async Task Like_is_idempotent_and_notifies_the_author_once()
    {
        var post = repository.Seed(author);
        users[reader] = ("Rajesh", "Kumar");
        var handler = new LikePostCommandHandler(repository, users, outbox);

        var first = await handler.Handle(new LikePostCommand(post.PostId, reader), CancellationToken.None);
        var second = await handler.Handle(new LikePostCommand(post.PostId, reader), CancellationToken.None);

        Assert.Equal(1, first.LikeCount);
        Assert.Equal(1, second.LikeCount);
        Assert.True(second.LikedByMe);
        var @event = Assert.IsType<PostLikedEvent>(Assert.Single(outbox.Events).Event);
        Assert.Equal(author, @event.PostAuthorUserId);
        Assert.Equal("Rajesh Kumar", @event.LikerName);
    }

    [Fact]
    public async Task Like_event_id_is_stable_so_relike_cannot_spam_the_author()
    {
        var post = repository.Seed(author);
        var like = new LikePostCommandHandler(repository, users, outbox);
        var unlike = new UnlikePostCommandHandler(repository);

        await like.Handle(new LikePostCommand(post.PostId, reader), CancellationToken.None);
        var afterUnlike = await unlike.Handle(new UnlikePostCommand(post.PostId, reader), CancellationToken.None);
        await like.Handle(new LikePostCommand(post.PostId, reader), CancellationToken.None);

        Assert.Equal(0, afterUnlike.LikeCount);
        var ids = outbox.Events.Select(e => ((PostLikedEvent)e.Event).EventId).Distinct();
        Assert.Single(ids); // NotificationService keeps one notification per (EventId, recipient)
    }

    [Fact]
    public async Task Liking_your_own_post_sends_no_notification()
    {
        var post = repository.Seed(author);

        await new LikePostCommandHandler(repository, users, outbox).Handle(new LikePostCommand(post.PostId, author), CancellationToken.None);

        Assert.Empty(outbox.Events);
        Assert.Single(repository.Likes);
    }

    [Fact]
    public async Task Liking_a_deleted_post_is_not_found()
    {
        var post = repository.Seed(author);
        post.IsDeleted = true;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new LikePostCommandHandler(repository, users, outbox)
            .Handle(new LikePostCommand(post.PostId, reader), CancellationToken.None));
    }

    // ----- Comments -----

    [Fact]
    public async Task Comment_on_someone_elses_post_notifies_the_author()
    {
        var post = repository.Seed(author);
        users[reader] = ("Priya", "Sharma");

        var dto = await new CreateCommentCommandHandler(repository, users, outbox)
            .Handle(new CreateCommentCommand(post.PostId, reader, "Great work on the plastering"), CancellationToken.None);

        var @event = Assert.IsType<PostCommentedEvent>(Assert.Single(outbox.Events).Event);
        Assert.Equal(author, @event.PostAuthorUserId);
        Assert.Equal(dto.CommentId, @event.CommentId);
        Assert.True(dto.CanEdit);
        Assert.True(dto.CanDelete);
    }

    [Fact]
    public async Task Only_the_comment_author_can_edit_but_the_post_author_can_delete()
    {
        var post = repository.Seed(author);
        var comment = await new CreateCommentCommandHandler(repository, users, outbox)
            .Handle(new CreateCommentCommand(post.PostId, reader, "First"), CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() => new UpdateCommentCommandHandler(repository, users)
            .Handle(new UpdateCommentCommand(comment.CommentId, author, "Edited by someone else"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => new DeleteCommentCommandHandler(repository)
            .Handle(new DeleteCommentCommand(comment.CommentId, Guid.NewGuid()), CancellationToken.None));

        var edited = await new UpdateCommentCommandHandler(repository, users).Handle(new UpdateCommentCommand(comment.CommentId, reader, "Edited"), CancellationToken.None);
        Assert.True(edited.IsEdited);

        await new DeleteCommentCommandHandler(repository).Handle(new DeleteCommentCommand(comment.CommentId, author), CancellationToken.None);
        Assert.Empty(repository.Comments);
    }

    [Fact]
    public async Task Comment_flags_reflect_the_viewer()
    {
        var post = repository.Seed(author);
        await new CreateCommentCommandHandler(repository, users, outbox).Handle(new CreateCommentCommand(post.PostId, reader, "Hi"), CancellationToken.None);
        var stranger = Guid.NewGuid();

        var asStranger = Assert.Single(await new GetPostCommentQueryHandler(repository, users).Handle(new GetPostCommentQuery(post.PostId, stranger), CancellationToken.None));
        var asPostAuthor = Assert.Single(await new GetPostCommentQueryHandler(repository, users).Handle(new GetPostCommentQuery(post.PostId, author), CancellationToken.None));

        Assert.False(asStranger.CanEdit || asStranger.CanDelete);
        Assert.False(asPostAuthor.CanEdit);
        Assert.True(asPostAuthor.CanDelete);
    }

    // ----- Soft delete and feed -----

    [Fact]
    public async Task Deleted_post_disappears_from_feed_and_comments()
    {
        var post = repository.Seed(author);
        await new DeletePostCommandHandler(repository).Handle(new DeletePostCommand(post.PostId, author), CancellationToken.None);

        Assert.True(post.IsDeleted);
        Assert.Contains(post, repository.Posts); // still stored
        var feed = await Feed(new FixedConnections([]), FeedScope.All);
        Assert.Empty(feed);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new GetPostCommentQueryHandler(repository, users).Handle(new GetPostCommentQuery(post.PostId, author), CancellationToken.None));
    }

    [Fact]
    public async Task Network_feed_shows_my_posts_and_my_connections_posts_only()
    {
        var friend = Guid.NewGuid();
        var mine = repository.Seed(reader, createdAt: DateTime.UtcNow.AddMinutes(-2));
        var friends = repository.Seed(friend, createdAt: DateTime.UtcNow.AddMinutes(-1));
        repository.Seed(Guid.NewGuid()); // stranger

        var feed = await Feed(new FixedConnections([friend]), FeedScope.Network);

        Assert.Equal([friends.PostId, mine.PostId], feed.Select(p => p.Id));
        Assert.True(feed.Single(p => p.Id == mine.PostId).IsMine);
    }

    [Fact]
    public async Task Network_feed_falls_back_to_everyone_when_connection_service_is_down()
    {
        repository.Seed(Guid.NewGuid());
        repository.Seed(Guid.NewGuid());

        var feed = await Feed(new FixedConnections(null), FeedScope.Network);

        Assert.Equal(2, feed.Count);
    }

    [Fact]
    public void Unknown_feed_scope_is_invalid() =>
        Assert.False(new GetFeedValidator().Validate(new GetFeedQuery(reader, "friends-of-friends", 1, 20)).IsValid);

    // ----- Notifications -----

    [Fact]
    public void Post_events_notify_the_post_author()
    {
        var liked = new PostLikedEvent(Guid.NewGuid(), Guid.NewGuid(), author, reader, "Rajesh Kumar", "Brick wall finished", DateTime.UtcNow);
        var commented = new PostCommentedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), author, reader, "Priya Sharma", "Nice", DateTime.UtcNow);

        var likeNote = Assert.Single(NotificationEventMapper.Map(EventTopics.PostLiked, JsonSerializer.Serialize(liked, Json)));
        var commentNote = Assert.Single(NotificationEventMapper.Map(EventTopics.PostCommented, JsonSerializer.Serialize(commented, Json)));

        Assert.Equal(author, likeNote.RecipientUserId);
        Assert.Equal("PostLiked", likeNote.Type);
        Assert.Equal(author, commentNote.RecipientUserId);
        Assert.Contains("Priya Sharma", commentNote.Message);
    }

    // ----- Helpers -----

    private Task<IReadOnlyList<FeedPostDto>> Feed(IConnectionServiceClient connections, string scope) =>
        new GetFeedQueryHandler(repository, users, connections, NullLogger<GetFeedQueryHandler>.Instance)
            .Handle(new GetFeedQuery(reader, scope, 1, 20), CancellationToken.None);

    private sealed class RecordingOutbox : IEventOutbox
    {
        public List<(string Topic, string Key, object Event)> Events { get; } = [];
        public void Add<TEvent>(string topic, string key, TEvent @event) where TEvent : notnull => Events.Add((topic, key, @event));
    }

    private sealed class NamedUsers : Dictionary<Guid, (string First, string Last)>, IUserServiceClient
    {
        public Task<Dictionary<Guid, UserSummaryDto>> GetUsersAsync(IEnumerable<Guid> autherIds, CancellationToken cancellationToken) =>
            Task.FromResult(autherIds.Where(ContainsKey).Distinct().ToDictionary(id => id, id => new UserSummaryDto { Id = id, FirstName = this[id].First, LastName = this[id].Last }));
    }

    private sealed class FixedConnections(IReadOnlyList<Guid>? ids) : IConnectionServiceClient
    {
        public Task<IReadOnlyList<Guid>?> GetMyConnectionIdsAsync(CancellationToken cancellationToken) => Task.FromResult(ids);
    }
}
