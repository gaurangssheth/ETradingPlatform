namespace OrderService.Application.Commands;

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public Task<TResult> SendAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken)
    {
        var handler = serviceProvider
            .GetRequiredService<ICommandHandler<TCommand, TResult>>();

        return handler.HandleAsync(command, cancellationToken);
    }
}