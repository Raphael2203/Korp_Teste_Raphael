namespace BillingService.Models;

public enum InvoiceStatus
{
    /// <summary>Nota aberta: aceita alterações e pode ser impressa.</summary>
    Open,

    /// <summary>Nota fechada: já impressa, com o estoque baixado.</summary>
    Closed
}
