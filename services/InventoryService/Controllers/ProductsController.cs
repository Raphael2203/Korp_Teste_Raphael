using InventoryService.Data;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly InventoryDbContext _context;
    public ProductsController(InventoryDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAll()
    {
        var products = await _context.Products
            .AsNoTracking()
            .ToListAsync();

        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Code))
            return BadRequest("O código é obrigatório.");
        
        if (string.IsNullOrWhiteSpace(product.Description))
            return BadRequest("A descrição é obrigatória.");

        if (product.Stock < 0)
            return BadRequest("O saldo não pode ser negativo.");

        var codeAlreadyExists = await _context.Products
            .AnyAsync(p => p.Code == product.Code);

        if (codeAlreadyExists)
            return Conflict("Já existe um produto com esse código.");

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new {id = product.Id},
            product
        );
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return NotFound();

        return Ok(product);
    }
}