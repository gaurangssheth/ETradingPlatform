using Grpc.Core;
using NServiceBus.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Configuration
{
    public static class OrderServiceRecoverabilityPolicy
    {
        public static RecoverabilityAction Invoke(
            RecoverabilityConfig config,
            ErrorContext context)
        {
            var exception = FindRpcException(context.Exception);

            if (exception != null && IsPermanent(exception.StatusCode))
            {
                return RecoverabilityAction.MoveToError(config.Failed.ErrorQueue);
            }

            return DefaultRecoverabilityPolicy.Invoke(config, context);
        }

        private static bool IsPermanent(StatusCode statusCode)
        {
            return statusCode is
                StatusCode.InvalidArgument or
                StatusCode.PermissionDenied or
                StatusCode.Unauthenticated;
        }

        private static RpcException? FindRpcException(Exception exception)
        {
            Exception? currentException = exception;

            while (currentException is not null)
            {
                if (currentException is RpcException rpcException)
                {
                    return rpcException;
                }
                currentException = currentException.InnerException;
            }
            
            return null;
        }
    }
}
