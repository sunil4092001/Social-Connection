using ConnectionService.Application;
using ConnectionService.Application.Commands;
using NotificationService.Application;
using PostService.Application;
using PostService.Application.Features.Posts.Commands.CreatePost;
using UserService.Application.Profiles;

namespace LinkedIn.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void User_profile_requires_display_name()
    {
        var validator = new CreateUserProfileValidator();

        var result = validator.Validate(new CreateUserProfileCommand("","F","L", "Engineer", null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Post_requires_author_and_content()
    {
        var validator = new CreatePostValidator();

        var result = validator.Validate(new CreatePostCommand(Guid.Empty, ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Connection_request_cannot_target_self()
    {
        var userId = Guid.NewGuid();
        var validator = new RequestConnectionValidator();

        var result = validator.Validate(new RequestConnectionCommand(userId, userId));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Notification_requires_recipient_type_and_message()
    {
        var validator = new CreateNotificationValidator();

        var result = validator.Validate(new CreateNotificationCommand(Guid.Empty, "", "", "", null, null));

        Assert.False(result.IsValid);
    }
}
