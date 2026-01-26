using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Dtos;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Controllers;

[ApiController]
[Route("api/recipes/{recipeId:guid}/snapshots")]
public class RecipeSnapshotsController : ControllerBase
{
    private readonly CostingDbContext _context;

    public RecipeSnapshotsController(CostingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecipeCostSnapshotDto>>> GetAll(
        Guid recipeId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var recipeExists = await _context.Recipes.AnyAsync(r => r.Id == recipeId);
        if (!recipeExists)
            return NotFound(new { message = "Recipe not found" });

        var query = _context.RecipeCostSnapshots
            .Where(s => s.RecipeId == recipeId);

        if (startDate.HasValue)
            query = query.Where(s => s.SnapshotDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(s => s.SnapshotDate <= endDate.Value);

        var snapshots = await query
            .OrderByDescending(s => s.SnapshotDate)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(snapshots.Select(MapToDto).ToList());
    }

    [HttpGet("{snapshotId:guid}")]
    public async Task<ActionResult<RecipeCostSnapshotDto>> GetById(Guid recipeId, Guid snapshotId)
    {
        var snapshot = await _context.RecipeCostSnapshots
            .FirstOrDefaultAsync(s => s.RecipeId == recipeId && s.Id == snapshotId);

        if (snapshot == null)
            return NotFound();

        var dto = MapToDto(snapshot);
        dto.AddSelfLink($"/api/recipes/{recipeId}/snapshots/{snapshotId}");

        return Ok(dto);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<RecipeCostSnapshotDto>> GetLatest(Guid recipeId)
    {
        var snapshot = await _context.RecipeCostSnapshots
            .Where(s => s.RecipeId == recipeId)
            .OrderByDescending(s => s.SnapshotDate)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (snapshot == null)
            return NotFound();

        var dto = MapToDto(snapshot);
        dto.AddSelfLink($"/api/recipes/{recipeId}/snapshots/latest");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeCostSnapshotDto>> Create(
        Guid recipeId,
        [FromBody] CreateSnapshotRequest request)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null)
            return NotFound(new { message = "Recipe not found" });

        var totalCost = recipe.Ingredients.Sum(i => i.CurrentLineCost);
        var costPerPortion = recipe.PortionYield > 0 ? totalCost / recipe.PortionYield : totalCost;
        var costPercent = request.MenuPrice > 0 ? (costPerPortion / request.MenuPrice) * 100 : 0;

        var snapshot = new RecipeCostSnapshot
        {
            RecipeId = recipeId,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalIngredientCost = totalCost,
            CostPerPortion = costPerPortion,
            PortionYield = recipe.PortionYield,
            MenuPrice = request.MenuPrice,
            CostPercentage = costPercent,
            GrossMarginPercent = 100 - costPercent,
            SnapshotReason = request.SnapshotReason
        };

        _context.RecipeCostSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        var dto = MapToDto(snapshot);
        dto.AddSelfLink($"/api/recipes/{recipeId}/snapshots/{snapshot.Id}");

        return CreatedAtAction(nameof(GetById),
            new { recipeId, snapshotId = snapshot.Id }, dto);
    }

    [HttpGet("compare")]
    public async Task<ActionResult<object>> Compare(
        Guid recipeId,
        [FromQuery] DateOnly date1,
        [FromQuery] DateOnly date2)
    {
        var snapshot1 = await _context.RecipeCostSnapshots
            .Where(s => s.RecipeId == recipeId && s.SnapshotDate == date1)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        var snapshot2 = await _context.RecipeCostSnapshots
            .Where(s => s.RecipeId == recipeId && s.SnapshotDate == date2)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (snapshot1 == null || snapshot2 == null)
            return NotFound(new { message = "One or both snapshots not found" });

        var costChange = snapshot2.CostPerPortion - snapshot1.CostPerPortion;
        var costChangePercent = snapshot1.CostPerPortion > 0
            ? (costChange / snapshot1.CostPerPortion) * 100
            : 0;

        return Ok(new
        {
            Date1 = date1,
            Date2 = date2,
            Snapshot1 = MapToDto(snapshot1),
            Snapshot2 = MapToDto(snapshot2),
            CostChange = costChange,
            CostChangePercent = costChangePercent,
            MarginChange = snapshot2.GrossMarginPercent - snapshot1.GrossMarginPercent
        });
    }

    private static RecipeCostSnapshotDto MapToDto(RecipeCostSnapshot snapshot)
    {
        return new RecipeCostSnapshotDto
        {
            Id = snapshot.Id,
            RecipeId = snapshot.RecipeId,
            SnapshotDate = snapshot.SnapshotDate,
            TotalIngredientCost = snapshot.TotalIngredientCost,
            CostPerPortion = snapshot.CostPerPortion,
            PortionYield = snapshot.PortionYield,
            MenuPrice = snapshot.MenuPrice,
            CostPercentage = snapshot.CostPercentage,
            GrossMarginPercent = snapshot.GrossMarginPercent,
            SnapshotReason = snapshot.SnapshotReason
        };
    }
}
