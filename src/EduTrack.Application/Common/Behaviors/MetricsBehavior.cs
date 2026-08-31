using EduTrack.Application.Abstractions.Observability;
using EduTrack.Application.Studies.Commands.AddOwnGrade;
using EduTrack.Application.Studies.Commands.CreateOwnAssignment;
using EduTrack.Application.Studies.Commands.UpdateOwnGrade;
using EduTrack.Application.Users.Commands.BindUser;
using EduTrack.Domain.Common;
using MediatR;

namespace EduTrack.Application.Common.Behaviors;

/// <summary>
/// Emits business metrics for the handful of commands 
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationMetrics _metrics;

    public MetricsBehavior(IApplicationMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        if (response is Result { IsSuccess: true })
        {
            Record(request);
        }

        return response;
    }

    private void Record(TRequest request)
    {
        switch (request)
        {
            case BindUserCommand:
                _metrics.AccountLinked();
                break;
            case AddOwnGradeCommand:
                _metrics.GradeAdded();
                break;
            case UpdateOwnGradeCommand:
                _metrics.GradeUpdated();
                break;
            case CreateOwnAssignmentCommand:
                _metrics.DeadlineCreated();
                break;
        }
    }
}
