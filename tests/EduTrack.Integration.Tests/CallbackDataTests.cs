using EduTrack.Bot.Web.Telegram;
using FluentAssertions;

namespace EduTrack.Integration.Tests;

/// <summary>
/// Unit coverage for the inline-button callback payloads: the builders produce the
/// compact wire format the modules parse, and ids survive a build/parse round-trip.
/// </summary>
public sealed class CallbackDataTests
{
    [Fact]
    public void ViewSubject_round_trips_the_subject_id_and_page()
    {
        var subjectId = Guid.NewGuid();

        var data = CallbackData.ViewSubject(subjectId, 3);
        var parts = CallbackData.Parts(data);

        parts.Should().HaveCount(4);
        parts[0].Should().Be(CallbackData.ViewNamespace);
        parts[1].Should().Be("sub");
        Guid.Parse(parts[2]).Should().Be(subjectId);
        int.Parse(parts[3]).Should().Be(3);
    }

    [Fact]
    public void AdminSetRole_round_trips_the_user_id_and_role()
    {
        var userId = Guid.NewGuid();

        var data = CallbackData.AdminSetRole(userId, 1);

        data.Should().StartWith(CallbackData.AdminWizardNamespace + ":");
        var parts = CallbackData.Parts(data);
        parts[1].Should().Be("role");
        Guid.Parse(parts[2]).Should().Be(userId);
        int.Parse(parts[3]).Should().Be(1);
    }

    [Theory]
    [InlineData("gw:val:5", CallbackData.WizardNamespace)]
    [InlineData("dw:sub:abc", CallbackData.DeadlineWizardNamespace)]
    [InlineData("av:menu", CallbackData.AdminViewNamespace)]
    public void Namespace_prefix_identifies_the_owning_module(string data, string expectedNamespace)
        => data.Split(':')[0].Should().Be(expectedNamespace);
}
