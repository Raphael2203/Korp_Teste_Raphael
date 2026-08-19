namespace BillingService.Models;

public class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    public int ProductId { get; set; }

    /// <summary>
    /// Código e descrição são copiados do estoque no momento da criação da nota.
    /// A nota preserva os dados do produto como estavam quando foi emitida.
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    public string ProductDescription { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
