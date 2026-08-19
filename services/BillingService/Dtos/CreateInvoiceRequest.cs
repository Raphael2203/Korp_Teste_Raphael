using System.ComponentModel.DataAnnotations;

namespace BillingService.Dtos;

public class CreateInvoiceRequest
{
    [Required(ErrorMessage = "Informe ao menos um produto na nota.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um produto na nota.")]
    public List<CreateInvoiceItemRequest> Items { get; set; } = [];
}

public class CreateInvoiceItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "O produto informado é inválido.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantity { get; set; }
}
