using System.ComponentModel.DataAnnotations;

namespace InventoryService.Dtos;

public class CreateProductRequest
{
    [Required(ErrorMessage = "O código é obrigatório.")]
    [MaxLength(50, ErrorMessage = "O código deve ter no máximo 50 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [MaxLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "O saldo não pode ser negativo.")]
    public int Stock { get; set; }
}
