namespace BillingService.Domain;

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? PaidAtUtc { get; set; }
    public void Complete(DateTime paidAtUtc)
    {
        if (Status == "Completed")
        {
            return;
        }

        Status = "Completed";
        PaidAtUtc = paidAtUtc;
    }
}
