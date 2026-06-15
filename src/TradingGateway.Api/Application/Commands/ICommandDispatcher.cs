namespace OrderService.Application.Commands;

public interface ICommandDispatcher
{
    Task<TResult> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken);
}