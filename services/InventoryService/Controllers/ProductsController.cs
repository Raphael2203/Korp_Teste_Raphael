using InventoryService.Ai;
using InventoryService.Data;
using InventoryService.Dtos;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly ProductDescriptionAssistant _descriptionAssistant;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        InventoryDbContext context,
        ProductDescriptionAssistant descriptionAssistant,
        ILogger<ProductsController> logger)
    {
        _context = context;
        _descriptionAssistant = descriptionAssistant;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAll()
    {
        var products = await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new ProductResponse
            {
                Id = p.Id,
                Code = p.Code,
                Description = p.Description,
                Stock = p.Stock
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Produto não encontrado.",
                Detail = $"Não existe produto com o identificador {id}."
            });

        return Ok(ProductResponse.FromProduct(product));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request)
    {
        var codeAlreadyExists = await _context.Products
            .AnyAsync(p => p.Code == request.Code);

        if (codeAlreadyExists)
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Código já cadastrado.",
                Detail = $"Já existe um produto com o código {request.Code}."
            });

        var product = new Product
        {
            Code = request.Code,
            Description = request.Description,
            Stock = request.Stock
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            ProductResponse.FromProduct(product)
        );
    }

    /// <summary>
    /// Baixa o saldo dos produtos informados. Operação atômica e idempotente:
    /// ou todos os itens são debitados, ou nenhum é.
    /// Chamada pelo BillingService durante a impressão da nota fiscal.
    /// </summary>
    [HttpPost("stock/consume")]
    [ProducesResponseType(typeof(ConsumeStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConsumeStockResponse>> ConsumeStock(ConsumeStockRequest request)
    {
        var duplicatedProduct = request.Items
            .GroupBy(i => i.ProductId)
            .Any(g => g.Count() > 1);

        if (duplicatedProduct)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Itens inválidos.",
                Detail = "O mesmo produto foi informado mais de uma vez."
            });

        var alreadyApplied = await _context.StockOperations
            .AnyAsync(o => o.OperationKey == request.OperationKey);

        if (alreadyApplied)
        {
            _logger.LogInformation(
                "Operação {OperationKey} já processada anteriormente; nenhuma baixa aplicada.",
                request.OperationKey
            );

            return Ok(await BuildResponseAsync(
                ConsumeStockResponse.AlreadyApplied,
                request
            ));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Ordena por identificador para manter a mesma sequência de locks
            // entre requisições concorrentes e evitar deadlock no PostgreSQL.
            var items = request.Items
                .OrderBy(i => i.ProductId)
                .ToList();

            foreach (var item in items)
            {
                var quantity = item.Quantity;

                // UPDATE atômico com a própria condição de saldo: se outra
                // requisição consumiu o estoque no meio do caminho, nenhuma
                // linha é afetada e a operação inteira é revertida.
                var affected = await _context.Products
                    .Where(p => p.Id == item.ProductId && p.Stock >= quantity)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.Stock, p => p.Stock - quantity));

                if (affected == 0)
                {
                    await transaction.RollbackAsync();

                    return await BuildUnavailableStockResultAsync(item);
                }
            }

            _context.StockOperations.Add(new StockOperation
            {
                OperationKey = request.OperationKey,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Duas requisições com a mesma chave chegaram juntas: o índice
            // único barrou a segunda. O resultado esperado já foi aplicado.
            await transaction.RollbackAsync();

            return Ok(await BuildResponseAsync(
                ConsumeStockResponse.AlreadyApplied,
                request
            ));
        }

        _logger.LogInformation(
            "Baixa de estoque {OperationKey} aplicada em {Count} produto(s).",
            request.OperationKey,
            request.Items.Count
        );

        return Ok(await BuildResponseAsync(
            ConsumeStockResponse.Applied,
            request
        ));
    }

    /// <summary>
    /// Sugere uma descrição comercial para o produto usando IA (opcional).
    /// </summary>
    [HttpPost("description-suggestion")]
    [ProducesResponseType(typeof(DescriptionSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DescriptionSuggestionResponse>> SuggestDescription(
        DescriptionSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        if (!_descriptionAssistant.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Assistente de IA não configurado.",
                Detail = "Defina a variável de ambiente ANTHROPIC_API_KEY para habilitar a sugestão de descrição."
            });

        string? suggestion;

        try
        {
            suggestion = await _descriptionAssistant.SuggestDescriptionAsync(
                request.Code,
                request.Draft,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao consultar o assistente de IA.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Assistente de IA indisponível.",
                Detail = "Não foi possível gerar a sugestão agora. Preencha a descrição manualmente."
            });
        }

        if (suggestion is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Sugestão não gerada.",
                Detail = "O assistente não retornou uma descrição. Preencha a descrição manualmente."
            });

        return Ok(new DescriptionSuggestionResponse { Suggestion = suggestion });
    }

    private async Task<ActionResult<ConsumeStockResponse>> BuildUnavailableStockResultAsync(
        ConsumeStockItem item)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == item.ProductId);

        if (product is null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Produto não encontrado.",
                Detail = $"O produto {item.ProductId} não existe mais no estoque."
            });

        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Saldo insuficiente.",
            Detail = $"O produto {product.Code} - {product.Description} possui saldo {product.Stock} e a nota utiliza {item.Quantity}."
        });
    }

    private async Task<ConsumeStockResponse> BuildResponseAsync(
        string status,
        ConsumeStockRequest request)
    {
        var productIds = request.Items
            .Select(i => i.ProductId)
            .ToList();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductResponse
            {
                Id = p.Id,
                Code = p.Code,
                Description = p.Description,
                Stock = p.Stock
            })
            .ToListAsync();

        return new ConsumeStockResponse
        {
            Status = status,
            OperationKey = request.OperationKey,
            Products = products
        };
    }
}
