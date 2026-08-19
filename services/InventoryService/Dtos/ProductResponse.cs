using InventoryService.Models;

namespace InventoryService.Dtos;

public class ProductResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Stock { get; set; }

    public static ProductResponse FromProduct(Product product) => new()
    {
        Id = product.Id,
        Code = product.Code,
        Description = product.Description,
        Stock = product.Stock
    };
}
