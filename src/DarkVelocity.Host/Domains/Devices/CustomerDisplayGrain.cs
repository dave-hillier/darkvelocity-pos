using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for customer-facing display management.
/// Manages configuration, pairing, and status for displays
/// mounted at POS terminals.
/// </summary>
public class CustomerDisplayGrain : Grain, ICustomerDisplayGrain
{
    private readonly IPersistentState<CustomerDisplayState> _state;

    public CustomerDisplayGrain(
        [PersistentState("customerDisplay", "OrleansStorage")]
        IPersistentState<CustomerDisplayState> state)
    {
        _state = state;
    }

    public async Task<CustomerDisplaySnapshot> RegisterAsync(RegisterCustomerDisplayCommand command)
    {
        if (_state.State.DisplayId != Guid.Empty)
            throw new InvalidOperationException("Customer display already registered");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var displayId = Guid.Parse(parts[2]);

        _state.State = new CustomerDisplayState
        {
            OrgId = orgId,
            DisplayId = displayId,
            LocationId = command.LocationId,
            Name = command.Name,
            DeviceId = command.DeviceId,
            PairedPosDeviceId = command.PairedPosDeviceId,
            IsActive = true,
            IsOnline = true,
            RegisteredAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<CustomerDisplaySnapshot> UpdateAsync(UpdateCustomerDisplayCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.PairedPosDeviceId.HasValue) _state.State.PairedPosDeviceId = command.PairedPosDeviceId.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;
        if (command.IdleMessage != null) _state.State.IdleMessage = command.IdleMessage;
        if (command.LogoUrl != null) _state.State.LogoUrl = command.LogoUrl;
        if (command.TipPresets != null) _state.State.TipPresets = command.TipPresets.ToList();
        if (command.TipEnabled.HasValue) _state.State.TipEnabled = command.TipEnabled.Value;
        if (command.ReceiptPromptEnabled.HasValue) _state.State.ReceiptPromptEnabled = command.ReceiptPromptEnabled.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        _state.State.IsOnline = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<CustomerDisplaySnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task RecordHeartbeatAsync()
    {
        EnsureInitialized();
        _state.State.IsOnline = true;
        _state.State.LastSeenAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetOfflineAsync()
    {
        EnsureInitialized();
        _state.State.IsOnline = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<bool> IsOnlineAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.IsOnline);
    }

    public async Task PairAsync(Guid posDeviceId)
    {
        EnsureInitialized();
        _state.State.PairedPosDeviceId = posDeviceId;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UnpairAsync()
    {
        EnsureInitialized();
        _state.State.PairedPosDeviceId = null;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetModeAsync(CustomerDisplayMode mode)
    {
        EnsureInitialized();
        _state.State.CurrentMode = mode;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private CustomerDisplaySnapshot CreateSnapshot()
    {
        return new CustomerDisplaySnapshot(
            DisplayId: _state.State.DisplayId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            DeviceId: _state.State.DeviceId,
            PairedPosDeviceId: _state.State.PairedPosDeviceId,
            IsActive: _state.State.IsActive,
            IsOnline: _state.State.IsOnline,
            LastSeenAt: _state.State.LastSeenAt,
            RegisteredAt: _state.State.RegisteredAt,
            IdleMessage: _state.State.IdleMessage,
            LogoUrl: _state.State.LogoUrl,
            TipPresets: _state.State.TipPresets.AsReadOnly(),
            TipEnabled: _state.State.TipEnabled,
            ReceiptPromptEnabled: _state.State.ReceiptPromptEnabled,
            CurrentMode: _state.State.CurrentMode);
    }

    private void EnsureInitialized()
    {
        if (_state.State.DisplayId == Guid.Empty)
            throw new InvalidOperationException("Customer display grain not initialized");
    }
}
