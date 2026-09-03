using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Common.Behaviors;
using EduTrack.Application.Users;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Domain.Common;
using NSubstitute;

namespace EduTrack.Application.Tests.Observability;

public class MetricsBehaviorTests
{
    private readonly IApplicationMetrics _metrics = Substitute.For<IApplicationMetrics>();

    private static UserProfileDto Profile() =>
        new(Guid.NewGuid(), 42, "tester", "Test User", "Student", "UTC", "ru", true, DateTime.UtcNow);

    [Fact]
    public async Task Records_the_business_metric_on_a_successful_command()
    {
        var behavior = new MetricsBehavior<BindUserCommand, Result<UserProfileDto>>(_metrics);
        var command = new BindUserCommand(42, "tester", "Test", null, "CODE");

        await behavior.Handle(command, () => Task.FromResult(Result.Success(Profile())), CancellationToken.None);

        _metrics.Received(1).AccountLinked();
    }

    [Fact]
    public async Task Does_not_record_when_the_command_fails()
    {
        var behavior = new MetricsBehavior<BindUserCommand, Result<UserProfileDto>>(_metrics);
        var command = new BindUserCommand(42, "tester", "Test", null, "CODE");

        await behavior.Handle(
            command,
            () => Task.FromResult(Result.Failure<UserProfileDto>(Error.NotFound("X", "nope"))),
            CancellationToken.None);

        _metrics.DidNotReceive().AccountLinked();
    }

    [Fact]
    public async Task Ignores_requests_it_does_not_track()
    {
        var behavior = new MetricsBehavior<Untracked, Result>(_metrics);

        await behavior.Handle(new Untracked(), () => Task.FromResult(Result.Success()), CancellationToken.None);

        _metrics.DidNotReceive().AccountLinked();
        _metrics.DidNotReceive().GradeAdded();
        _metrics.DidNotReceive().GradeUpdated();
        _metrics.DidNotReceive().DeadlineCreated();
    }

    private sealed record Untracked;
}
