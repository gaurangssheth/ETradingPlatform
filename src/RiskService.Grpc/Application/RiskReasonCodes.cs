namespace RiskService.Grpc.Application
{
    public class RiskReasonCodes
    {
        public const string Approved = "APPROVED";
        public const string InvalidOrderId = "INVALID_ORDER_ID";
        public const string ClientNotActive = "CLIENT_NOT_ACTIVE";
        public const string ClientBlocked = "CLIENT_BLOCKED";
        public const string SymbolNotAllowed = "SYMBOL_NOT_ALLOWED";
        public const string InvalidQuantity = "INVALID_QUANTITY";
        public const string MaxOrderSizeExceeded = "MAX_ORDER_SIZE_EXCEEDED";
    }
}

