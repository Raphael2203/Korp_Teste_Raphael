using System.ComponentModel.DataAnnotations;

namespace InventoryService.Dtos;

public class ConsumeStockRequest
{
    /// <summary>
    /// Identificador único da operação. Repetir a mesma chave não gera
    /// uma segunda baixa de estoque (idempotência).
    /// </summary>
    [Required(ErrorMessage = "A chave da operação é obrigatória.")]
    [MaxLength(100)]
    public string OperationKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Os itens são obrigatórios.")]
    [MinLength(1, ErrorMessage = "Informe pelo menos um item.")]
    public List<ConsumeStockItem> Items { get; set; } = [];
}

public class ConsumeStockItem
{
    [Range(1, int.MaxValue, ErrorMessage = "O produto informado é inválido.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantity { get; set; }
}
