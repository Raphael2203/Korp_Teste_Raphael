using BillingService.Clients;
using BillingService.Data;
using BillingService.Dtos;
using BillingService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InvoicesController : ControllerBase
{
    private readonly BillingDbContext _context;
    private readonly InventoryClient _inventoryClient;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        BillingDbContext context,
        InventoryClient inventoryClient,
        ILogger<InvoicesController> logger)
    {
        _context = context;
        _inventoryClient = inventoryClient;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InvoiceResponse>>> GetAll()
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .OrderByDescending(i => i.Number)
            .ToListAsync();

        return Ok(invoices.Select(InvoiceResponse.FromInvoice));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> GetById(int id)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null)
            return InvoiceNotFound(id);

        return Ok(InvoiceResponse.FromInvoice(invoice));
    }

    /// <summary>
    /// Cria uma nota fiscal com um ou mais produtos. A nota nasce com status
    /// Aberta e numeração sequencial gerada pelo banco.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var duplicatedProduct = request.Items
            .GroupBy(item => item.ProductId)
            .Any(group => group.Count() > 1);

        if (duplicatedProduct)
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Itens inválidos.",
                Detail = "O mesmo produto foi informado mais de uma vez na nota."
            });

        var invoice = new Invoice
        {
            Status = InvoiceStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        // Cada produto é validado no serviço de estoque e seus dados são
        // copiados para a nota (snapshot no momento da emissão).
        foreach (var item in request.Items)
        {
            var lookup = await _inventoryClient.GetProductAsync(
                item.ProductId,
                cancellationToken
            );

            if (!lookup.IsSuccess)
                return MapInventoryFailure(lookup.Outcome, lookup.Detail);

            var product = lookup.Value!;

            invoice.Items.Add(new InvoiceItem
            {
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductDescription = product.Description,
                Quantity = item.Quantity
            });
        }

        _context.Invoices.Add(invoice);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Nota fiscal {Number} criada com {Count} item(ns).",
            invoice.Number,
            invoice.Items.Count
        );

        return CreatedAtAction(
            nameof(GetById),
            new { id = invoice.Id },
            InvoiceResponse.FromInvoice(invoice)
        );
    }

    /// <summary>
    /// Imprime (fecha) a nota fiscal: valida o saldo, baixa o estoque no
    /// InventoryService e só então muda o status para Fechada.
    ///
    /// Se o estoque falhar — por saldo insuficiente ou por indisponibilidade do
    /// microsserviço — a nota permanece Aberta e nada é debitado.
    /// </summary>
    [HttpPost("{id:int}/print")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvoiceResponse>> Print(
        int id,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
            return InvoiceNotFound(id);

        // Impede a segunda impressão: somente notas Abertas podem ser impressas.
        if (invoice.Status != InvoiceStatus.Open)
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Nota já impressa.",
                Detail = $"A nota {invoice.Number} está {TranslateStatus(invoice.Status)} e não pode ser impressa novamente."
            });

        var items = invoice.Items
            .Select(item => new ConsumeStockItemRequest(item.ProductId, item.Quantity))
            .ToList();

        // A chave é derivada da própria nota: um retry desta operação nunca
        // baixa o estoque duas vezes.
        var operationKey = $"invoice-{invoice.Id}";

        var consume = await _inventoryClient.ConsumeStockAsync(
            operationKey,
            items,
            cancellationToken
        );

        if (!consume.IsSuccess)
        {
            _logger.LogWarning(
                "Impressão da nota {Number} não concluída ({Outcome}). A nota permanece Aberta.",
                invoice.Number,
                consume.Outcome
            );

            return MapInventoryFailure(consume.Outcome, consume.Detail);
        }

        // O estoque confirmou a baixa: agora sim a nota é fechada.
        invoice.Status = InvoiceStatus.Closed;
        invoice.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Nota fiscal {Number} impressa e fechada (baixa de estoque: {Status}).",
            invoice.Number,
            consume.Value!.Status
        );

        return Ok(InvoiceResponse.FromInvoice(invoice));
    }

    private ActionResult InvoiceNotFound(int id) =>
        NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Nota fiscal não encontrada.",
            Detail = $"Não existe nota fiscal com o identificador {id}."
        });

    /// <summary>
    /// Traduz a falha vinda do estoque no código HTTP e na mensagem que o
    /// usuário verá na tela.
    /// </summary>
    private ActionResult MapInventoryFailure(InventoryOutcome outcome, string? detail) =>
        outcome switch
        {
            InventoryOutcome.ProductNotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Produto não encontrado.",
                Detail = detail ?? "Um dos produtos da nota não existe no estoque."
            }),

            InventoryOutcome.InsufficientStock => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Saldo insuficiente.",
                Detail = detail ?? "Não há saldo suficiente para os produtos da nota."
            }),

            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Serviço de estoque indisponível.",
                Detail = detail ?? "Não foi possível falar com o serviço de estoque. A nota permanece Aberta; tente novamente em instantes."
            })
        };

    private static string TranslateStatus(InvoiceStatus status) =>
        status == InvoiceStatus.Open ? "Aberta" : "Fechada";
}
