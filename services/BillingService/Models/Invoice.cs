namespace BillingService.Models;

public class Invoice
{
    public int Id { get; set; }

    /// <summary>
    /// Numeração sequencial da nota, gerada por sequence do PostgreSQL.
    /// </summary>
    public int Number { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public List<InvoiceItem> Items { get; set; } = [];
}
