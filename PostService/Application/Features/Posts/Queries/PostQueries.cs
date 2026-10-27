using FluentValidation;
using MediatR;
using PostService.Application.Features.Posts.DTOs.FeedPost;
using PostService.Application.Interfaces;

namespace PostService.Application.Features.Posts.Queries;

public static class FeedScope
{
    /// <summary>My own posts and posts from my connections (default).</summary>
    public const string Network = "network";
    /// <summary>Everyone's posts.</summary>
    public const string All = "all";
}

// ----- Feed -----

public sealed record GetFeedQuery(Guid ViewerId, string Scope, int Page, int PageSize) : IRequest<IReadOnlyList<FeedPostDto>>;

public sealed class GetFeedValidator : AbstractValidator<GetFeedQuery>
{
    public GetFeedValidator()
    {
        RuleFor(query => query.ViewerId).NotEmpty();
        RuleFor(query => query.Scope).Must(scope => scope is FeedScope.Network or FeedScope.All).WithMessage("Scope must be 'network' or 'all'.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetFeedQueryHandler(
    IPostRepository repository,
    IUserServiceClient users,
    IConnectionServiceClient connections,
    ILogger<GetFeedQueryHandler> logger) : IRequestHandler<GetFeedQuery, IReadOnlyList<FeedPostDto>>
{
    public async Task<IReadOnlyList<FeedPostDto>> Handle(GetFeedQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<Guid>? authorIds = null;
        if (request.Scope == FeedScope.Network)
        {
            var connectionIds = await connections.GetMyConnectionIdsAsync(cancellationToken);
            if (connectionIds is null)
            {
                // The feed must keep working when ConnectionService is down: show everyone's posts instead.
                logger.LogWarning("ConnectionService unavailable; serving the public feed to {UserId}", request.ViewerId);
            }
            else
            {
                authorIds = [request.ViewerId, .. connectionIds];
            }
        }

        var views = await repository.GetPostViewsAsync(authorIds, request.ViewerId, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
        return await PostPresentation.ToDtosAsync(views, request.ViewerId, users, cancellationToken);
    }
}

// ----- Posts of one user (profile page) -----

public sealed record GetUserPostsQuery(Guid ViewerId, Guid AuthorUserId, int Page, int PageSize) : IRequest<IReadOnlyList<FeedPostDto>>;

public sealed class GetUserPostsValidator : AbstractValidator<GetUserPostsQuery>
{
    public GetUserPostsValidator()
    {
        RuleFor(query => query.ViewerId).NotEmpty();
        RuleFor(query => query.AuthorUserId).NotEmpty();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetUserPostsQueryHandler(IPostRepository repository, IUserServiceClient users) : IRequestHandler<GetUserPostsQuery, IReadOnlyList<FeedPostDto>>
{
    public async Task<IReadOnlyList<FeedPostDto>> Handle(GetUserPostsQuery request, CancellationToken cancellationToken)
    {
        var views = await repository.GetPostViewsAsync([request.AuthorUserId], request.ViewerId, (request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
        return await PostPresentation.ToDtosAsync(views, request.ViewerId, users, cancellationToken);
    }
}

// ----- One post -----

public sealed record GetPostByIdQuery(Guid PostId, Guid ViewerId) : IRequest<FeedPostDto?>;

public sealed class GetPostByIdValidator : AbstractValidator<GetPostByIdQuery>
{
    public GetPostByIdValidator()
    {
        RuleFor(query => query.PostId).NotEmpty();
        RuleFor(query => query.ViewerId).NotEmpty();
    }
}

public sealed class GetPostByIdQueryHandler(IPostRepository repository, IUserServiceClient users) : IRequestHandler<GetPostByIdQuery, FeedPostDto?>
{
    public async Task<FeedPostDto?> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
    {
        var view = await repository.GetPostViewAsync(request.PostId, request.ViewerId, cancellationToken);
        return view is null ? null : (await PostPresentation.ToDtosAsync([view], request.ViewerId, users, cancellationToken))[0];
    }
}
