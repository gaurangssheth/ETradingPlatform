using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TradingApp.Shared.Diagnostics;

public sealed class InlineSqlLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<InlineSqlLoggingInterceptor> logger;

    public InlineSqlLoggingInterceptor(
        ILogger<InlineSqlLoggingInterceptor> logger)
    {
        this.logger = logger;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        LogSql(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        LogSql(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        LogSql(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        LogSql(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void LogSql(DbCommand command)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var sql = command.CommandText;

        var parameters = command.Parameters
            .Cast<DbParameter>()
            .OrderByDescending(p => p.ParameterName.Length);

        foreach (var parameter in parameters)
        {
            sql = Regex.Replace(
                sql,
                $@"(?<!\w){Regex.Escape(parameter.ParameterName)}(?!\w)",
                FormatValue(parameter.Value));
        }

        logger.LogInformation("EF inline SQL:{NewLine}{Sql}", Environment.NewLine, sql);
    }

    private static string FormatValue(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return "NULL";
        }

        return value switch
        {
            string text => "N'" + text.Replace("'", "''") + "'",

            Guid guid => "'" + guid + "'",

            DateTime dateTime => "'" +
                dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) +
                "'",

            DateTimeOffset dateTimeOffset => "'" +
                dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture) +
                "'",

            bool boolean => boolean ? "1" : "0",

            decimal number => number.ToString(CultureInfo.InvariantCulture),

            double number => number.ToString(CultureInfo.InvariantCulture),

            float number => number.ToString(CultureInfo.InvariantCulture),

            int number => number.ToString(CultureInfo.InvariantCulture),

            long number => number.ToString(CultureInfo.InvariantCulture),

            short number => number.ToString(CultureInfo.InvariantCulture),

            byte[] bytes => "0x" + Convert.ToHexString(bytes),

            _ => "N'" + value.ToString()!.Replace("'", "''") + "'"
        };
    }
}