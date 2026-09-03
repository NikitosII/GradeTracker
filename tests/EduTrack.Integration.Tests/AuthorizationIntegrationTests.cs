using EduTrack.Application.Studies;
using EduTrack.Application.Studies.Commands.UpdateOwnGrade;
using EduTrack.Application.Studies.Queries.GetOwnGrades;
using EduTrack.Domain.Studies;
using EduTrack.Domain.Users;
using EduTrack.Infrastructure.Persistence;
using EduTrack.Integration.Tests.TestSupport;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EduTrack.Integration.Tests;

/// <summary>
/// Exercises the data-ownership rules (spec 18.4 / 22.4) through the real MediatR
/// pipeline and a real Postgres database: a student may only touch and see their
/// own grades.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthorizationIntegrationTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly EduTrackWebAppFactory _factory;

    public AuthorizationIntegrationTests(PostgresFixture postgres)
    {
        _factory = new EduTrackWebAppFactory(postgres.ConnectionString);
        _factory.CreateClient();
    }

    [Fact]
    public async Task Student_cannot_edit_another_students_grade()
    {
        var (gradeId, _) = await SeedOwnedGradeAsync(ownerTelegramId: 7011, otherTelegramId: 7012);

        var result = await SendAsync(new UpdateOwnGradeCommand(7012, gradeId, 5, 1m, "hijack", Now));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GradeErrors.NotOwner);
    }

    [Fact]
    public async Task Student_can_edit_their_own_grade()
    {
        var (gradeId, _) = await SeedOwnedGradeAsync(ownerTelegramId: 7021, otherTelegramId: 7022);

        var result = await SendAsync(new UpdateOwnGradeCommand(7021, gradeId, 5, 2m, "fixed", Now));

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(5);
    }

    [Fact]
    public async Task Student_cannot_see_another_students_grades()
    {
        var (_, subjectId) = await SeedOwnedGradeAsync(ownerTelegramId: 7031, otherTelegramId: 7032);

        // The other (bound) student queries the same subject and sees nothing.
        var result = await SendAsync(new GetOwnGradesQuery(7032, subjectId, Page: 1, PageSize: 10));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
    }

    private async Task<(Guid GradeId, Guid SubjectId)> SeedOwnedGradeAsync(long ownerTelegramId, long otherTelegramId)
        => await _factory.WithScopeAsync(async db =>
        {
            var owner = User.Register(ownerTelegramId, "owner", "Owner", null, UserRole.Student, Now);
            var other = User.Register(otherTelegramId, "other", "Other", null, UserRole.Student, Now);
            var subject = Subject.Create($"Subject-{ownerTelegramId}", null, Now);
            var grade = Grade.Add(owner.Id, subject.Id, 3, 1m, "own", Now, owner.Id, Now);

            db.Users.Add(owner);
            db.Users.Add(other);
            db.Subjects.Add(subject);
            db.Grades.Add(grade);
            await db.SaveChangesAsync();

            return (grade.Id, subject.Id);
        });

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    public void Dispose() => _factory.Dispose();
}
