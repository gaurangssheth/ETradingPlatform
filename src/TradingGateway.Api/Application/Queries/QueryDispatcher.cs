namespace OrderService.Application.Queries;

public sealed class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider serviceProvider;

    public QueryDispatcher(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public Task<TResult> SendAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken)
    {
        var handler = serviceProvider
            .GetRequiredService<IQueryHandler<TQuery, TResult>>();

        return handler.HandleAsync(query, cancellationToken);
    }
}