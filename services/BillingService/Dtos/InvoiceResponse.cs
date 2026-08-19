using BillingService.Models;

namespace BillingService.Dtos;

public class InvoiceResponse
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<InvoiceItemResponse> Items { get; set; } = [];

    public static InvoiceResponse FromInvoice(Invoice invoice) => new()
    {
        Id = invoice.Id,
        Number = invoice.Number,
        Status = invoice.Status.ToString(),
        CreatedAt = invoice.CreatedAt,
        ClosedAt = invoice.ClosedAt,
        Items = invoice.Items
            .Select(item => new InvoiceItemResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductCode = item.ProductCode,
                ProductDescription = item.ProductDescription,
                Quantity = item.Quantity
            })
            .ToList()
    };
}

public class InvoiceItemResponse
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
