using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Supplier Grain
// ============================================================================

/// <summary>
/// Grain for supplier management.
/// Manages supplier information, pricing, and performance metrics.
/// Uses event sourcing via JournaledGrain for state persistence.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class SupplierGrain : JournaledGrain<SupplierState, ISupplierEvent>, ISupplierGrain
{
    private readonly IGrainFactory _grainFactory;

    public SupplierGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    protected override void TransitionState(SupplierState state, ISupplierEvent @event)
    {
        switch (@event)
        {
            case SupplierCreated e:
                state.OrgId = e.OrgId;
                state.SupplierId = e.SupplierId;
                state.Code = e.Code;
                state.Name = e.Name;
                state.ContactName = e.ContactName ?? "";
                state.ContactEmail = e.ContactEmail ?? "";
                state.ContactPhone = e.ContactPhone ?? "";
                state.Address = e.Address ?? "";
                state.PaymentTermsDays = e.PaymentTermsDays;
                state.LeadTimeDays = e.LeadTimeDays;
                state.Notes = e.Notes;
                state.IsActive = true;
                break;

            case SupplierUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.ContactName != null) state.ContactName = e.ContactName;
                if (e.ContactEmail != null) state.ContactEmail = e.ContactEmail;
                if (e.ContactPhone != null) state.ContactPhone = e.ContactPhone;
                if (e.Address != null) state.Address = e.Address;
                if (e.PaymentTermsDays.HasValue) state.PaymentTermsDays = e.PaymentTermsDays.Value;
                if (e.LeadTimeDays.HasValue) state.LeadTimeDays = e.LeadTimeDays.Value;
                if (e.Notes != null) state.Notes = e.Notes;
                if (e.IsActive.HasValue) state.IsActive = e.IsActive.Value;
                break;

            case SupplierIngredientAdded e:
                var existingIngredient = state.Ingredients.FirstOrDefault(i => i.IngredientId == e.IngredientId);
                if (existingIngredient != null)
                {
                    existingIngredient.SupplierSku = e.SupplierSku ?? "";
                    existingIngredient.UnitPrice = e.UnitPrice;
                    existingIngredient.Unit = e.Unit;
                    existingIngredient.MinOrderQuantity = e.MinOrderQuantity ?? 0;
                    existingIngredient.LeadTimeDays = e.LeadTimeDays ?? 0;
                }
                else
                {
                    state.Ingredients.Add(new SupplierIngredientState
                    {
                        IngredientId = e.IngredientId,
                        IngredientName = e.IngredientName,
                        Sku = e.Sku ?? "",
                        SupplierSku = e.SupplierSku ?? "",
                        UnitPrice = e.UnitPrice,
                        Unit = e.Unit,
                        MinOrderQuantity = e.MinOrderQuantity ?? 0,
                        LeadTimeDays = e.LeadTimeDays ?? 0
                    });
                }
                break;

            case SupplierIngredientRemoved e:
                state.Ingredients.RemoveAll(i => i.IngredientId == e.IngredientId);
                break;

            case SupplierIngredientPriceUpdated e:
                var ingredientToUpdate = state.Ingredients.FirstOrDefault(i => i.IngredientId == e.IngredientId);
                if (ingredientToUpdate != null)
                {
                    ingredientToUpdate.UnitPrice = e.NewPrice;
                }
                break;

            case SupplierPurchaseRecorded e:
                state.TotalPurchasesYtd += e.Amount;
                state.TotalDeliveries++;
                if (e.OnTime) state.OnTimeDeliveries++;
                break;
        }
    }

    public async Task<SupplierSnapshot> CreateAsync(CreateSupplierCommand command)
    {
        if (State.SupplierId != Guid.Empty)
            throw new InvalidOperationException("Supplier already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, _, supplierId) = GrainKeys.ParseOrgEntity(key);

        RaiseEvent(new SupplierCreated
        {
            SupplierId = supplierId,
            OrgId = orgId,
            Code = command.Code,
            Name = command.Name,
            ContactName = command.ContactName,
            ContactEmail = command.ContactEmail,
            ContactPhone = command.ContactPhone,
            Address = command.Address,
            PaymentTermsDays = command.PaymentTermsDays,
            LeadTimeDays = command.LeadTimeDays,
            Notes = command.Notes,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();

        var snapshot = CreateSnapshot();
        await GetRegistry().RegisterSupplierAsync(snapshot);
        return snapshot;
    }

    public async Task<SupplierSnapshot> UpdateAsync(UpdateSupplierCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierUpdated
        {
            SupplierId = State.SupplierId,
            Name = command.Name,
            ContactName = command.ContactName,
            ContactEmail = command.ContactEmail,
            ContactPhone = command.ContactPhone,
            Address = command.Address,
            PaymentTermsDays = command.PaymentTermsDays,
            LeadTimeDays = command.LeadTimeDays,
            Notes = command.Notes,
            IsActive = command.IsActive,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();

        var snapshot = CreateSnapshot();
        await GetRegistry().UpdateSupplierAsync(snapshot);
        return snapshot;
    }

    public async Task AddIngredientAsync(SupplierIngredient ingredient)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierIngredientAdded
        {
            SupplierId = State.SupplierId,
            IngredientId = ingredient.IngredientId,
            IngredientName = ingredient.IngredientName,
            Sku = ingredient.Sku,
            SupplierSku = ingredient.SupplierSku,
            UnitPrice = ingredient.UnitPrice,
            Unit = ingredient.Unit,
            MinOrderQuantity = ingredient.MinOrderQuantity,
            LeadTimeDays = ingredient.LeadTimeDays,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RemoveIngredientAsync(Guid ingredientId)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierIngredientRemoved
        {
            SupplierId = State.SupplierId,
            IngredientId = ingredientId,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateIngredientPriceAsync(Guid ingredientId, decimal newPrice)
    {
        EnsureInitialized();

        var ingredient = State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredientId)
            ?? throw new InvalidOperationException("Ingredient not found");

        RaiseEvent(new SupplierIngredientPriceUpdated
        {
            SupplierId = State.SupplierId,
            IngredientId = ingredientId,
            NewPrice = newPrice,
            PreviousPrice = ingredient.UnitPrice,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<SupplierSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<decimal> GetIngredientPriceAsync(Guid ingredientId)
    {
        EnsureInitialized();

        var ingredient = State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredientId)
            ?? throw new InvalidOperationException("Ingredient not found");

        return Task.FromResult(ingredient.UnitPrice);
    }

    public async Task RecordPurchaseAsync(decimal amount, bool onTime)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierPurchaseRecorded
        {
            SupplierId = State.SupplierId,
            Amount = amount,
            OnTime = onTime,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    private SupplierSnapshot CreateSnapshot()
    {
        var onTimePercent = State.TotalDeliveries > 0
            ? State.OnTimeDeliveries * 100 / State.TotalDeliveries
            : 100;

        return new SupplierSnapshot(
            SupplierId: State.SupplierId,
            Code: State.Code,
            Name: State.Name,
            ContactName: State.ContactName,
            ContactEmail: State.ContactEmail,
            ContactPhone: State.ContactPhone,
            Address: State.Address,
            PaymentTermsDays: State.PaymentTermsDays,
            LeadTimeDays: State.LeadTimeDays,
            Notes: State.Notes,
            IsActive: State.IsActive,
            Ingredients: State.Ingredients.Select(i => new SupplierIngredient(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Sku: i.Sku,
                SupplierSku: i.SupplierSku,
                UnitPrice: i.UnitPrice,
                Unit: i.Unit,
                MinOrderQuantity: i.MinOrderQuantity,
                LeadTimeDays: i.LeadTimeDays)).ToList(),
            TotalPurchasesYtd: State.TotalPurchasesYtd,
            OnTimeDeliveryPercent: onTimePercent);
    }

    private ISupplierRegistryGrain GetRegistry()
    {
        return _grainFactory.GetGrain<ISupplierRegistryGrain>(
            GrainKeys.SupplierRegistry(State.OrgId));
    }

    private void EnsureInitialized()
    {
        if (State.SupplierId == Guid.Empty)
            throw new InvalidOperationException("Supplier grain not initialized");
    }
}
