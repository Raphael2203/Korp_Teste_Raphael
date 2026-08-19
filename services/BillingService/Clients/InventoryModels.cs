namespace BillingService.Clients;

public record InventoryProduct(
    int Id,
    string Code,
    string Description,
    int Stock
);

public record ConsumeStockItemRequest(
    int ProductId,
    int Quantity
);

public record ConsumeStockPayload(
    string OperationKey,
    IReadOnlyList<ConsumeStockItemRequest> Items
);

public record ConsumeStockResult(
    string Status,
    string OperationKey,
    List<InventoryProduct> Products
);

/// <summary>
/// Resultado possível de uma chamada ao serviço de estoque.
/// </summary>
public enum InventoryOutcome
{
    /// <summary>A chamada foi concluída com sucesso.</summary>
    Success,

    /// <summary>O produto informado não existe no estoque.</summary>
    ProductNotFound,

    /// <summary>Não há saldo suficiente para atender a nota.</summary>
    InsufficientStock,

    /// <summary>O serviço de estoque está fora do ar ou não respondeu a tempo.</summary>
    Unavailable
}

/// <summary>
/// Encapsula o desfecho de uma chamada ao InventoryService, para que o
/// controller decida o código HTTP sem precisar tratar exceções de rede.
/// </summary>
public record InventoryResponse<T>(
    InventoryOutcome Outcome,
    T? Value = default,
    string? Detail = null)
{
    public bool IsSuccess => Outcome == InventoryOutcome.Success;

    public static InventoryResponse<T> Success(T value) =>
        new(InventoryOutcome.Success, value);

    public static InventoryResponse<T> Failure(InventoryOutcome outcome, string detail) =>
        new(outcome, default, detail);
}
