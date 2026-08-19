namespace InventoryService.Models;

/// <summary>
/// Registro das baixas de estoque já processadas.
/// A unicidade de <see cref="OperationKey"/> é o que garante a idempotência:
/// se a mesma operação for reenviada, o estoque não é debitado novamente.
/// </summary>
public class StockOperation
{
    public int Id { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
