namespace OrderService.Application.Queries;

public interface IQueryDispatcher
{
    Task<TResult> SendAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken);
}