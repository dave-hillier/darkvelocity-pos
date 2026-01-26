using DarkVelocity.Procurement.Api.Data;
using DarkVelocity.Procurement.Api.Dtos;
using DarkVelocity.Procurement.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Procurement.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly ProcurementDbContext _context;

    public SuppliersController(ProcurementDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<SupplierDto>>> GetAll([FromQuery] bool? activeOnly = null)
    {
        var query = _context.Suppliers.AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(s => s.IsActive);
        }

        var suppliers = await query
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                ContactName = s.ContactName,
                ContactEmail = s.ContactEmail,
                ContactPhone = s.ContactPhone,
                Address = s.Address,
                PaymentTermsDays = s.PaymentTermsDays,
                LeadTimeDays = s.LeadTimeDays,
                Notes = s.Notes,
                IsActive = s.IsActive,
                ProductCount = s.SupplierIngredients.Count(si => si.IsActive)
            })
            .ToListAsync();

        foreach (var supplier in suppliers)
        {
            supplier.AddSelfLink($"/api/suppliers/{supplier.Id}");
            supplier.AddLink("ingredients", $"/api/suppliers/{supplier.Id}/ingredients");
        }

        return Ok(HalCollection<SupplierDto>.Create(
            suppliers,
            "/api/suppliers",
            suppliers.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> GetById(Guid id)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.SupplierIngredients)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (supplier == null)
            return NotFound();

        var dto = new SupplierDto
        {
            Id = supplier.Id,
            Code = supplier.Code,
            Name = supplier.Name,
            ContactName = supplier.ContactName,
            ContactEmail = supplier.ContactEmail,
            ContactPhone = supplier.ContactPhone,
            Address = supplier.Address,
            PaymentTermsDays = supplier.PaymentTermsDays,
            LeadTimeDays = supplier.LeadTimeDays,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            ProductCount = supplier.SupplierIngredients.Count(si => si.IsActive)
        };

        dto.AddSelfLink($"/api/suppliers/{supplier.Id}");
        dto.AddLink("ingredients", $"/api/suppliers/{supplier.Id}/ingredients");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request)
    {
        var existingCode = await _context.Suppliers.AnyAsync(s => s.Code == request.Code);
        if (existingCode)
            return Conflict(new { message = "Supplier code already exists" });

        var supplier = new Supplier
        {
            Code = request.Code,
            Name = request.Name,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            PaymentTermsDays = request.PaymentTermsDays,
            LeadTimeDays = request.LeadTimeDays,
            Notes = request.Notes
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var dto = new SupplierDto
        {
            Id = supplier.Id,
            Code = supplier.Code,
            Name = supplier.Name,
            ContactName = supplier.ContactName,
            ContactEmail = supplier.ContactEmail,
            ContactPhone = supplier.ContactPhone,
            Address = supplier.Address,
            PaymentTermsDays = supplier.PaymentTermsDays,
            LeadTimeDays = supplier.LeadTimeDays,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            ProductCount = 0
        };

        dto.AddSelfLink($"/api/suppliers/{supplier.Id}");

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, [FromBody] UpdateSupplierRequest request)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.SupplierIngredients)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (supplier == null)
            return NotFound();

        if (request.Name != null)
            supplier.Name = request.Name;
        if (request.ContactName != null)
            supplier.ContactName = request.ContactName;
        if (request.ContactEmail != null)
            supplier.ContactEmail = request.ContactEmail;
        if (request.ContactPhone != null)
            supplier.ContactPhone = request.ContactPhone;
        if (request.Address != null)
            supplier.Address = request.Address;
        if (request.PaymentTermsDays.HasValue)
            supplier.PaymentTermsDays = request.PaymentTermsDays.Value;
        if (request.LeadTimeDays.HasValue)
            supplier.LeadTimeDays = request.LeadTimeDays.Value;
        if (request.Notes != null)
            supplier.Notes = request.Notes;
        if (request.IsActive.HasValue)
            supplier.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new SupplierDto
        {
            Id = supplier.Id,
            Code = supplier.Code,
            Name = supplier.Name,
            ContactName = supplier.ContactName,
            ContactEmail = supplier.ContactEmail,
            ContactPhone = supplier.ContactPhone,
            Address = supplier.Address,
            PaymentTermsDays = supplier.PaymentTermsDays,
            LeadTimeDays = supplier.LeadTimeDays,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
            ProductCount = supplier.SupplierIngredients.Count(si => si.IsActive)
        };

        dto.AddSelfLink($"/api/suppliers/{supplier.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);

        if (supplier == null)
            return NotFound();

        supplier.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Supplier Ingredients
    [HttpGet("{supplierId:guid}/ingredients")]
    public async Task<ActionResult<HalCollection<SupplierIngredientDto>>> GetIngredients(Guid supplierId)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
            return NotFound();

        var ingredients = await _context.SupplierIngredients
            .Where(si => si.SupplierId == supplierId)
            .Select(si => new SupplierIngredientDto
            {
                Id = si.Id,
                SupplierId = si.SupplierId,
                IngredientId = si.IngredientId,
                SupplierProductCode = si.SupplierProductCode,
                SupplierProductName = si.SupplierProductName,
                PackSize = si.PackSize,
                PackUnit = si.PackUnit,
                LastKnownPrice = si.LastKnownPrice,
                LastPriceUpdatedAt = si.LastPriceUpdatedAt,
                IsPreferred = si.IsPreferred,
                IsActive = si.IsActive
            })
            .ToListAsync();

        foreach (var ingredient in ingredients)
        {
            ingredient.AddSelfLink($"/api/suppliers/{supplierId}/ingredients/{ingredient.Id}");
        }

        return Ok(HalCollection<SupplierIngredientDto>.Create(
            ingredients,
            $"/api/suppliers/{supplierId}/ingredients",
            ingredients.Count
        ));
    }

    [HttpPost("{supplierId:guid}/ingredients")]
    public async Task<ActionResult<SupplierIngredientDto>> AddIngredient(
        Guid supplierId,
        [FromBody] AddSupplierIngredientRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
            return NotFound();

        var existingLink = await _context.SupplierIngredients
            .AnyAsync(si => si.SupplierId == supplierId && si.IngredientId == request.IngredientId);
        if (existingLink)
            return Conflict(new { message = "Ingredient already linked to this supplier" });

        var supplierIngredient = new SupplierIngredient
        {
            SupplierId = supplierId,
            IngredientId = request.IngredientId,
            SupplierProductCode = request.SupplierProductCode,
            SupplierProductName = request.SupplierProductName,
            PackSize = request.PackSize,
            PackUnit = request.PackUnit,
            LastKnownPrice = request.LastKnownPrice,
            LastPriceUpdatedAt = DateTime.UtcNow,
            IsPreferred = request.IsPreferred
        };

        _context.SupplierIngredients.Add(supplierIngredient);
        await _context.SaveChangesAsync();

        var dto = new SupplierIngredientDto
        {
            Id = supplierIngredient.Id,
            SupplierId = supplierIngredient.SupplierId,
            IngredientId = supplierIngredient.IngredientId,
            SupplierProductCode = supplierIngredient.SupplierProductCode,
            SupplierProductName = supplierIngredient.SupplierProductName,
            PackSize = supplierIngredient.PackSize,
            PackUnit = supplierIngredient.PackUnit,
            LastKnownPrice = supplierIngredient.LastKnownPrice,
            LastPriceUpdatedAt = supplierIngredient.LastPriceUpdatedAt,
            IsPreferred = supplierIngredient.IsPreferred,
            IsActive = supplierIngredient.IsActive
        };

        dto.AddSelfLink($"/api/suppliers/{supplierId}/ingredients/{supplierIngredient.Id}");

        return Created($"/api/suppliers/{supplierId}/ingredients/{supplierIngredient.Id}", dto);
    }

    [HttpPut("{supplierId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<ActionResult<SupplierIngredientDto>> UpdateIngredient(
        Guid supplierId,
        Guid ingredientId,
        [FromBody] UpdateSupplierIngredientRequest request)
    {
        var supplierIngredient = await _context.SupplierIngredients
            .FirstOrDefaultAsync(si => si.SupplierId == supplierId && si.Id == ingredientId);

        if (supplierIngredient == null)
            return NotFound();

        if (request.LastKnownPrice.HasValue)
        {
            supplierIngredient.LastKnownPrice = request.LastKnownPrice.Value;
            supplierIngredient.LastPriceUpdatedAt = DateTime.UtcNow;
        }
        if (request.SupplierProductCode != null)
            supplierIngredient.SupplierProductCode = request.SupplierProductCode;
        if (request.SupplierProductName != null)
            supplierIngredient.SupplierProductName = request.SupplierProductName;
        if (request.PackSize.HasValue)
            supplierIngredient.PackSize = request.PackSize.Value;
        if (request.PackUnit != null)
            supplierIngredient.PackUnit = request.PackUnit;
        if (request.IsPreferred.HasValue)
            supplierIngredient.IsPreferred = request.IsPreferred.Value;
        if (request.IsActive.HasValue)
            supplierIngredient.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new SupplierIngredientDto
        {
            Id = supplierIngredient.Id,
            SupplierId = supplierIngredient.SupplierId,
            IngredientId = supplierIngredient.IngredientId,
            SupplierProductCode = supplierIngredient.SupplierProductCode,
            SupplierProductName = supplierIngredient.SupplierProductName,
            PackSize = supplierIngredient.PackSize,
            PackUnit = supplierIngredient.PackUnit,
            LastKnownPrice = supplierIngredient.LastKnownPrice,
            LastPriceUpdatedAt = supplierIngredient.LastPriceUpdatedAt,
            IsPreferred = supplierIngredient.IsPreferred,
            IsActive = supplierIngredient.IsActive
        };

        dto.AddSelfLink($"/api/suppliers/{supplierId}/ingredients/{supplierIngredient.Id}");

        return Ok(dto);
    }

    [HttpDelete("{supplierId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<IActionResult> RemoveIngredient(Guid supplierId, Guid ingredientId)
    {
        var supplierIngredient = await _context.SupplierIngredients
            .FirstOrDefaultAsync(si => si.SupplierId == supplierId && si.Id == ingredientId);

        if (supplierIngredient == null)
            return NotFound();

        _context.SupplierIngredients.Remove(supplierIngredient);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
