namespace TradingGateway.Api.Application.Queries;

public interface IQueryDispatcher
{
    Task<TResult> SendAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken);
}