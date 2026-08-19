using System.ComponentModel.DataAnnotations;

namespace InventoryService.Dtos;

public class DescriptionSuggestionRequest
{
    [Required(ErrorMessage = "Informe o código do produto.")]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe um rascunho da descrição.")]
    [MaxLength(200)]
    public string Draft { get; set; } = string.Empty;
}

public class DescriptionSuggestionResponse
{
    public string Suggestion { get; set; } = string.Empty;
}
