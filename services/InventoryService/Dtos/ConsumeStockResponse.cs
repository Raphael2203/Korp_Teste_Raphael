namespace InventoryService.Dtos;

public class ConsumeStockResponse
{
    /// <summary>
    /// "Applied" quando a baixa foi executada agora,
    /// "AlreadyApplied" quando a operação já havia sido processada antes.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public string OperationKey { get; set; } = string.Empty;

    public List<ProductResponse> Products { get; set; } = [];

    public const string Applied = "Applied";
    public const string AlreadyApplied = "AlreadyApplied";
}
