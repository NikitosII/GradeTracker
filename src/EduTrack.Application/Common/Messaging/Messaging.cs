using EduTrack.Domain.Common;
using MediatR;

namespace EduTrack.Application.Common.Messaging;

/// <summary>Command that changes state and returns no value.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>Command that changes state and returns a value.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// <summary>Query that reads state.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
