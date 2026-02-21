using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class DeviceEndpoints
{
    public static WebApplication MapDeviceEndpoints(this WebApplication app)
    {
        MapStationsEndpoints(app);
        MapDeviceAuthEndpoints(app);
        MapDeviceManagementEndpoints(app);
        MapPrintJobEndpoints(app);
        MapSyncQueueEndpoints(app);
        MapDeviceHealthEndpoints(app);
        MapCustomerDisplayEndpoints(app);

        return app;
    }

    private static void MapStationsEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/stations").WithTags("Stations");

        group.MapGet("/{orgId}/{siteId}", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var stations = new[]
            {
                new { id = Guid.NewGuid(), name = "Grill Station", siteId, orderTypes = new[] { "hot", "grill" } },
                new { id = Guid.NewGuid(), name = "Cold Station", siteId, orderTypes = new[] { "cold", "salad" } },
                new { id = Guid.NewGuid(), name = "Expeditor", siteId, orderTypes = new[] { "all" } },
                new { id = Guid.NewGuid(), name = "Bar", siteId, orderTypes = new[] { "drinks", "bar" } },
            };
            return Results.Ok(new { items = stations });
        });

        group.MapPost("/{orgId}/{siteId}/select", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SelectStationRequest request,
            IGrainFactory grainFactory) =>
        {
            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, request.DeviceId));
            if (!await deviceGrain.ExistsAsync())
                return Results.NotFound(new { error = "device_not_found" });

            return Results.Ok(new
            {
                message = "Station selected",
                deviceId = request.DeviceId,
                stationId = request.StationId,
                stationName = request.StationName
            });
        });
    }

    private static void MapDeviceAuthEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/device").WithTags("DeviceAuth");

        group.MapPost("/code", async (
            [FromBody] DeviceCodeApiRequest request,
            IGrainFactory grainFactory,
            HttpContext httpContext,
            IConfiguration configuration) =>
        {
            var userCode = GrainKeys.GenerateUserCode();
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(userCode);

            var backofficeUrl = configuration["App:BackofficeUrl"] ?? "http://localhost:5174";
            var verificationBaseUri = $"{backofficeUrl.TrimEnd('/')}/device";

            var response = await grain.InitiateAsync(new DeviceCodeRequest(
                request.ClientId,
                request.Scope ?? "device",
                request.DeviceFingerprint,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                verificationBaseUri
            ));

            return Results.Ok(response);
        });

        group.MapPost("/token", async (
            [FromBody] DeviceTokenApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
            var status = await grain.GetStatusAsync();

            return status switch
            {
                DeviceAuthStatus.Pending => Results.BadRequest(new { error = "authorization_pending", error_description = "The authorization request is still pending" }),
                DeviceAuthStatus.Expired => Results.BadRequest(new { error = "expired_token", error_description = "The device code has expired" }),
                DeviceAuthStatus.Denied => Results.BadRequest(new { error = "access_denied", error_description = "The authorization request was denied" }),
                DeviceAuthStatus.Authorized => Results.Ok(await grain.GetTokenAsync(request.DeviceCode)),
                _ => Results.BadRequest(new { error = "invalid_request" })
            };
        });

        group.MapPost("/authorize", async (
            [FromBody] AuthorizeDeviceApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());

            await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
                request.AuthorizedBy,
                request.OrganizationId,
                request.SiteId,
                request.DeviceName,
                request.AppType
            ));

            return Results.Ok(new { message = "Device authorized successfully" });
        });

        group.MapPost("/deny", async (
            [FromBody] DenyDeviceApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
            await grain.DenyAsync(request.Reason ?? "User denied authorization");
            return Results.Ok(new { message = "Device authorization denied" });
        });
    }

    private static void MapDeviceManagementEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("Devices");

        group.MapGet("/{orgId}/{deviceId}", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Device not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDeviceLinks(snapshot)));
        });

        group.MapPost("/{orgId}/{deviceId}/heartbeat", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] DeviceHeartbeatRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Device not found"));

            await grain.RecordHeartbeatAsync(request.AppVersion);
            return Results.Ok();
        });

        group.MapPost("/{orgId}/{deviceId}/suspend", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] SuspendDeviceRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Device not found"));

            await grain.SuspendAsync(request.Reason);
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDeviceLinks(snapshot)));
        });

        group.MapPost("/{orgId}/{deviceId}/revoke", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] RevokeDeviceRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Device not found"));

            await grain.RevokeAsync(request.Reason);
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDeviceLinks(snapshot)));
        });

        group.MapPost("/{orgId}/{deviceId}/reactivate", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Device not found"));

            await grain.ReactivateAsync();
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDeviceLinks(snapshot)));
        });
    }

    private static Dictionary<string, object> BuildDeviceLinks(DeviceSnapshot snapshot)
    {
        var orgId = snapshot.OrganizationId;
        var deviceId = snapshot.Id;
        var basePath = $"/api/devices/{orgId}/{deviceId}";

        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["organization"] = new { href = $"/api/orgs/{orgId}" },
            ["sessions"] = new { href = $"{basePath}/sessions" }
        };

        // Add site link if device is assigned to a site
        if (snapshot.SiteId != Guid.Empty)
        {
            links["site"] = new { href = $"/api/orgs/{orgId}/sites/{snapshot.SiteId}" };
        }

        // Add current user link if a user is logged in on this device
        if (snapshot.CurrentUserId.HasValue)
        {
            links["current-user"] = new { href = $"/api/orgs/{orgId}/users/{snapshot.CurrentUserId.Value}" };
        }

        // Add action links based on device status
        switch (snapshot.Status)
        {
            case DeviceStatus.Authorized:
                links["suspend"] = new { href = $"{basePath}/suspend", method = "POST" };
                links["revoke"] = new { href = $"{basePath}/revoke", method = "POST" };
                break;
            case DeviceStatus.Suspended:
                links["reactivate"] = new { href = $"{basePath}/reactivate", method = "POST" };
                links["revoke"] = new { href = $"{basePath}/revoke", method = "POST" };
                break;
            // Revoked devices have no action links - they cannot be reactivated
        }

        return links;
    }

    // ============================================================================
    // Print Job Endpoints
    // ============================================================================

    private static void MapPrintJobEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("PrintJobs");

        // Queue a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] QueuePrintJobApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IDevicePrintQueueGrain>(
                $"{orgId}:device:{deviceId}:printqueue");

            // Initialize queue if needed
            await queueGrain.InitializeAsync(deviceId);

            var command = new QueuePrintJobCommand(
                PrinterId: request.PrinterId,
                JobType: request.JobType,
                Content: request.Content,
                Copies: request.Copies ?? 1,
                Priority: request.Priority ?? 0,
                SourceOrderId: request.SourceOrderId,
                SourceReference: request.SourceReference);

            var snapshot = await queueGrain.EnqueueAsync(command);
            return Results.Created($"/api/devices/{orgId}/{deviceId}/print-jobs/{snapshot.JobId}", snapshot);
        });

        // List print jobs
        group.MapGet("/{orgId}/{deviceId}/print-jobs", async (
            Guid orgId,
            Guid deviceId,
            [FromQuery] bool? includeHistory,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IDevicePrintQueueGrain>(
                $"{orgId}:device:{deviceId}:printqueue");

            var summary = await queueGrain.GetSummaryAsync();

            if (includeHistory == true)
            {
                var history = await queueGrain.GetHistoryAsync(50);
                return Results.Ok(new
                {
                    summary.PendingJobs,
                    summary.PrintingJobs,
                    summary.CompletedJobs,
                    summary.FailedJobs,
                    ActiveJobs = summary.ActiveJobs,
                    History = history
                });
            }

            return Results.Ok(summary);
        });

        // Get specific print job
        group.MapGet("/{orgId}/{deviceId}/print-jobs/{jobId}", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });

        // Start a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/start", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.StartAsync(new StartPrintJobCommand());
            return Results.Ok(snapshot);
        });

        // Complete a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/complete", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            [FromBody] CompletePrintJobApiRequest? request,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.CompleteAsync(new CompletePrintJobCommand(request?.PrinterResponse));
            return Results.Ok(snapshot);
        });

        // Fail a print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/fail", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            [FromBody] FailPrintJobApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.FailAsync(new FailPrintJobCommand(request.ErrorMessage, request.ErrorCode));
            return Results.Ok(snapshot);
        });

        // Retry a failed print job
        group.MapPost("/{orgId}/{deviceId}/print-jobs/{jobId}/retry", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await jobGrain.RetryAsync();
            return Results.Ok(snapshot);
        });

        // Cancel a print job
        group.MapDelete("/{orgId}/{deviceId}/print-jobs/{jobId}", async (
            Guid orgId,
            Guid deviceId,
            Guid jobId,
            [FromQuery] string? reason,
            IGrainFactory grainFactory) =>
        {
            var jobGrain = grainFactory.GetGrain<IPrintJobGrain>(
                $"{orgId}:device:{deviceId}:printjob:{jobId}");

            if (!await jobGrain.ExistsAsync())
                return Results.NotFound();

            await jobGrain.CancelAsync(reason);
            return Results.NoContent();
        });
    }

    // ============================================================================
    // Sync Queue Endpoints
    // ============================================================================

    private static void MapSyncQueueEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("OfflineSync");

        // Queue an offline operation
        group.MapPost("/{orgId}/{deviceId}/sync-queue", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] QueueOfflineOperationApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            // Initialize queue if needed
            await queueGrain.InitializeAsync(deviceId);

            var command = new QueueOfflineOperationCommand(
                OperationType: request.OperationType,
                EntityType: request.EntityType,
                EntityId: request.EntityId,
                PayloadJson: request.PayloadJson,
                ClientTimestamp: request.ClientTimestamp,
                ClientSequence: request.ClientSequence,
                UserId: request.UserId,
                IdempotencyKey: request.IdempotencyKey);

            var snapshot = await queueGrain.QueueOperationAsync(command);
            return Results.Created($"/api/devices/{orgId}/{deviceId}/sync-queue/{snapshot.OperationId}", snapshot);
        });

        // Get sync queue summary
        group.MapGet("/{orgId}/{deviceId}/sync-queue", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var summary = await queueGrain.GetSummaryAsync();
            return Results.Ok(summary);
        });

        // Process (sync) the queue
        group.MapPost("/{orgId}/{deviceId}/sync", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var result = await queueGrain.ProcessQueueAsync();
            return Results.Ok(result);
        });

        // Get conflicted operations
        group.MapGet("/{orgId}/{deviceId}/sync-queue/conflicts", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var conflicts = await queueGrain.GetConflictedOperationsAsync();
            return Results.Ok(conflicts);
        });

        // Resolve a conflict
        group.MapPost("/{orgId}/{deviceId}/sync-queue/{operationId}/resolve", async (
            Guid orgId,
            Guid deviceId,
            Guid operationId,
            [FromBody] ResolveConflictApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            var command = new ResolveConflictCommand(
                OperationId: operationId,
                Strategy: request.Strategy,
                ResolvedPayloadJson: request.ResolvedPayloadJson,
                ResolvedBy: request.ResolvedBy);

            var snapshot = await queueGrain.ResolveConflictAsync(command);
            return Results.Ok(snapshot);
        });

        // Clear synced operations from history
        group.MapDelete("/{orgId}/{deviceId}/sync-queue/synced", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var queueGrain = grainFactory.GetGrain<IOfflineSyncQueueGrain>(
                $"{orgId}:device:{deviceId}:syncqueue");

            await queueGrain.ClearSyncedOperationsAsync();
            return Results.NoContent();
        });
    }

    // ============================================================================
    // Device Health Endpoints
    // ============================================================================

    private static void MapDeviceHealthEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("DeviceHealth");

        // Get device health for a specific device
        group.MapGet("/{orgId}/{siteId}/health/{deviceId}", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            var health = await statusGrain.GetDeviceHealthAsync(deviceId);
            if (health == null)
                return Results.NotFound();

            return Results.Ok(health);
        });

        // Get health summary for all devices at a site
        group.MapGet("/{orgId}/{siteId}/health", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            var summary = await statusGrain.GetHealthSummaryAsync();
            return Results.Ok(summary);
        });

        // Record device heartbeat with health metrics
        group.MapPost("/{orgId}/{siteId}/health/{deviceId}/heartbeat", async (
            Guid orgId,
            Guid siteId,
            Guid deviceId,
            [FromBody] DeviceHealthHeartbeatRequest? request,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            UpdateDeviceHealthCommand? healthUpdate = null;
            if (request != null)
            {
                healthUpdate = new UpdateDeviceHealthCommand(
                    DeviceId: deviceId,
                    SignalStrength: request.SignalStrength,
                    LatencyMs: request.LatencyMs,
                    PrinterStatus: request.PrinterStatus,
                    PaperLevel: request.PaperLevel,
                    PendingPrintJobs: request.PendingPrintJobs);
            }

            await statusGrain.RecordHeartbeatAsync(deviceId, healthUpdate);
            return Results.Ok(new { message = "Heartbeat recorded" });
        });

        // Perform health check (mark stale devices as offline)
        group.MapPost("/{orgId}/{siteId}/health/check", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int? offlineThresholdMinutes,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            var threshold = TimeSpan.FromMinutes(offlineThresholdMinutes ?? 5);
            var staleDevices = await statusGrain.PerformHealthCheckAsync(threshold);

            return Results.Ok(new
            {
                staleDevicesCount = staleDevices.Count,
                staleDevices
            });
        });

        // Update printer health status
        group.MapPost("/{orgId}/{siteId}/health/{printerId}/printer-status", async (
            Guid orgId,
            Guid siteId,
            Guid printerId,
            [FromBody] UpdatePrinterHealthRequest request,
            IGrainFactory grainFactory) =>
        {
            var statusGrain = grainFactory.GetGrain<IDeviceStatusGrain>(
                $"{orgId}:{siteId}:devicestatus");

            await statusGrain.UpdatePrinterHealthAsync(printerId, request.Status, request.PaperLevel);
            return Results.Ok(new { message = "Printer health updated" });
        });
    }

    // ============================================================================
    // Customer Display Endpoints
    // ============================================================================

    private static void MapCustomerDisplayEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("CustomerDisplay");

        // Register a customer display
        group.MapPost("/{orgId}/displays", async (
            Guid orgId,
            [FromBody] RegisterCustomerDisplayApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var displayId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            var command = new RegisterCustomerDisplayCommand(
                LocationId: request.LocationId,
                Name: request.Name,
                DeviceId: request.DeviceId,
                PairedPosDeviceId: request.PairedPosDeviceId);

            var snapshot = await grain.RegisterAsync(command);
            return Results.Created(
                $"/api/devices/{orgId}/displays/{displayId}",
                Hal.Resource(snapshot, BuildCustomerDisplayLinks(orgId, snapshot)));
        });

        // Get a customer display
        group.MapGet("/{orgId}/displays/{displayId}", async (
            Guid orgId,
            Guid displayId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, BuildCustomerDisplayLinks(orgId, snapshot)));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Customer display not found"));
            }
        });

        // Update a customer display
        group.MapPut("/{orgId}/displays/{displayId}", async (
            Guid orgId,
            Guid displayId,
            [FromBody] UpdateCustomerDisplayApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            try
            {
                var command = new UpdateCustomerDisplayCommand(
                    Name: request.Name,
                    PairedPosDeviceId: request.PairedPosDeviceId,
                    IsActive: request.IsActive,
                    IdleMessage: request.IdleMessage,
                    LogoUrl: request.LogoUrl,
                    TipPresets: request.TipPresets,
                    TipEnabled: request.TipEnabled,
                    ReceiptPromptEnabled: request.ReceiptPromptEnabled);

                var snapshot = await grain.UpdateAsync(command);
                return Results.Ok(Hal.Resource(snapshot, BuildCustomerDisplayLinks(orgId, snapshot)));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Customer display not found"));
            }
        });

        // Pair a display to a POS device
        group.MapPost("/{orgId}/displays/{displayId}/pair", async (
            Guid orgId,
            Guid displayId,
            [FromBody] PairDisplayApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            try
            {
                await grain.PairAsync(request.PosDeviceId);
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, BuildCustomerDisplayLinks(orgId, snapshot)));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Customer display not found"));
            }
        });

        // Unpair a display
        group.MapPost("/{orgId}/displays/{displayId}/unpair", async (
            Guid orgId,
            Guid displayId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            try
            {
                await grain.UnpairAsync();
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, BuildCustomerDisplayLinks(orgId, snapshot)));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Customer display not found"));
            }
        });

        // Record a heartbeat from the display
        group.MapPost("/{orgId}/displays/{displayId}/heartbeat", async (
            Guid orgId,
            Guid displayId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            try
            {
                await grain.RecordHeartbeatAsync();
                return Results.Ok(new { message = "Heartbeat recorded" });
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Customer display not found"));
            }
        });

        // Deactivate a display
        group.MapDelete("/{orgId}/displays/{displayId}", async (
            Guid orgId,
            Guid displayId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));

            try
            {
                await grain.DeactivateAsync();
                return Results.NoContent();
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Customer display not found"));
            }
        });
    }

    private static Dictionary<string, object> BuildCustomerDisplayLinks(Guid orgId, CustomerDisplaySnapshot snapshot)
    {
        var basePath = $"/api/devices/{orgId}/displays/{snapshot.DisplayId}";

        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["organization"] = new { href = $"/api/orgs/{orgId}" }
        };

        if (snapshot.PairedPosDeviceId.HasValue)
        {
            links["paired-device"] = new { href = $"/api/devices/{orgId}/{snapshot.PairedPosDeviceId.Value}" };
            links["unpair"] = new { href = $"{basePath}/unpair", method = "POST" };
        }
        else
        {
            links["pair"] = new { href = $"{basePath}/pair", method = "POST" };
        }

        if (snapshot.IsActive)
        {
            links["deactivate"] = new { href = basePath, method = "DELETE" };
        }

        return links;
    }
}

// ============================================================================
// API Request/Response Types
// ============================================================================

public record QueuePrintJobApiRequest(
    Guid PrinterId,
    PrintJobType JobType,
    string Content,
    int? Copies = 1,
    int? Priority = 0,
    Guid? SourceOrderId = null,
    string? SourceReference = null);

public record CompletePrintJobApiRequest(string? PrinterResponse = null);

public record FailPrintJobApiRequest(string ErrorMessage, string? ErrorCode = null);

public record QueueOfflineOperationApiRequest(
    OfflineOperationType OperationType,
    string EntityType,
    Guid EntityId,
    string PayloadJson,
    DateTime ClientTimestamp,
    long ClientSequence,
    Guid? UserId = null,
    string? IdempotencyKey = null);

public record ResolveConflictApiRequest(
    ConflictResolutionStrategy Strategy,
    string? ResolvedPayloadJson = null,
    Guid? ResolvedBy = null);

public record DeviceHealthHeartbeatRequest(
    int? SignalStrength = null,
    int? LatencyMs = null,
    PrinterHealthStatus? PrinterStatus = null,
    int? PaperLevel = null,
    int? PendingPrintJobs = null);

public record UpdatePrinterHealthRequest(
    PrinterHealthStatus Status,
    int? PaperLevel = null);

public record RegisterCustomerDisplayApiRequest(
    Guid LocationId,
    string Name,
    string DeviceId,
    Guid? PairedPosDeviceId = null);

public record UpdateCustomerDisplayApiRequest(
    string? Name = null,
    Guid? PairedPosDeviceId = null,
    bool? IsActive = null,
    string? IdleMessage = null,
    string? LogoUrl = null,
    IReadOnlyList<int>? TipPresets = null,
    bool? TipEnabled = null,
    bool? ReceiptPromptEnabled = null);

public record PairDisplayApiRequest(Guid PosDeviceId);
