using Accountrack.SharedKernel.Results;
using MediatR;

namespace Accountrack.Application.Abstractions.Messaging;

/// <summary>A state-changing use case that returns no value. Runs inside the write pipeline.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>A state-changing use case that returns a value.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// <summary>A read-only use case.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
