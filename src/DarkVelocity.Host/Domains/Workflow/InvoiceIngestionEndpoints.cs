using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class InvoiceIngestionEndpoints
{
    public static WebApplication MapIngestionAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/ingestion-agent")
            .WithTags("IngestionAgent");

        // Configure agent
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] ConfigureIngestionAgentRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (await grain.ExistsAsync())
                return Results.Conflict(Hal.Error("already_exists", "Agent already configured"));

            var snapshot = await grain.ConfigureAsync(new ConfigureIngestionAgentCommand(
                orgId,
                siteId,
                request.PollingIntervalMinutes ?? 5,
                request.AutoProcessEnabled ?? true,
                request.AutoProcessConfidenceThreshold ?? 0.85m));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent",
                Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Get agent status
        group.MapGet("/", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Update settings
        group.MapPatch("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] UpdateIngestionAgentSettingsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var snapshot = await grain.UpdateSettingsAsync(new UpdateIngestionAgentSettingsCommand(
                request.PollingIntervalMinutes,
                request.AutoProcessEnabled,
                request.AutoProcessConfidenceThreshold,
                request.SlackWebhookUrl,
                request.SlackNotifyOnNewItem));

            return Results.Ok(Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Activate
        group.MapPost("/activate", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var snapshot = await grain.ActivateAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Deactivate
        group.MapPost("/deactivate", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var snapshot = await grain.DeactivateAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Add mailbox
        group.MapPost("/mailboxes", async (
            Guid orgId,
            Guid siteId,
            [FromBody] AddMailboxRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var snapshot = await grain.AddMailboxAsync(new AddMailboxCommand(
                request.DisplayName,
                request.Host,
                request.Port,
                request.Username,
                request.Password,
                request.UseSsl ?? true,
                request.FolderName ?? "INBOX",
                request.DefaultDocumentType ?? PurchaseDocumentType.Invoice));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent",
                Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Remove mailbox
        group.MapDelete("/mailboxes/{configId}", async (
            Guid orgId,
            Guid siteId,
            Guid configId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var snapshot = await grain.RemoveMailboxAsync(configId);
            return Results.Ok(Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Test mailbox connection
        group.MapPost("/mailboxes/test", async (
            Guid orgId,
            Guid siteId,
            [FromBody] AddMailboxRequest request,
            [FromServices] IMailboxPollingService pollingService) =>
        {
            var config = new MailboxConnectionConfig
            {
                Host = request.Host,
                Port = request.Port,
                UseSsl = request.UseSsl ?? true,
                Username = request.Username,
                Password = request.Password,
                FolderName = request.FolderName ?? "INBOX"
            };

            var result = await pollingService.TestConnectionAsync(config);
            return Results.Ok(result);
        });

        // Trigger poll
        group.MapPost("/poll", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var result = await grain.TriggerPollAsync();
            return Results.Ok(result);
        });

        // Get pending queue
        group.MapGet("/queue", async (
            Guid orgId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var items = await grain.GetQueueAsync();
            var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent/queue";

            return Results.Ok(Hal.Collection(basePath, items.Cast<object>(), items.Count));
        });

        // Get history
        group.MapGet("/history", async (
            Guid orgId,
            Guid siteId,
            [FromQuery] int? limit,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var history = await grain.GetHistoryAsync(limit ?? 50);
            var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent/history";

            return Results.Ok(Hal.Collection(basePath, history.Cast<object>(), history.Count));
        });

        // Set routing rules
        group.MapPut("/routing-rules", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SetRoutingRulesRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Agent not configured"));

            var rules = request.Rules.Select(r => new RoutingRule
            {
                RuleId = Guid.NewGuid(),
                Name = r.Name,
                Type = r.Type,
                Pattern = r.Pattern,
                SuggestedDocumentType = r.SuggestedDocumentType,
                SuggestedVendorId = r.SuggestedVendorId,
                SuggestedVendorName = r.SuggestedVendorName,
                AutoApprove = r.AutoApprove,
                Priority = r.Priority
            }).ToList();

            var snapshot = await grain.SetRoutingRulesAsync(new SetRoutingRulesCommand(rules));
            return Results.Ok(Hal.Resource(snapshot, BuildAgentLinks(orgId, siteId)));
        });

        // Plan endpoints
        var planGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/ingestion-agent/plans")
            .WithTags("IngestionAgent");

        // Get plan
        planGroup.MapGet("/{planId}", async (
            Guid orgId,
            Guid siteId,
            Guid planId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDocumentProcessingPlanGrain>(
                GrainKeys.DocumentProcessingPlan(orgId, siteId, planId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Plan not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildPlanLinks(orgId, siteId, planId)));
        });

        // Approve plan
        planGroup.MapPost("/{planId}/approve", async (
            Guid orgId,
            Guid siteId,
            Guid planId,
            [FromBody] ApprovePlanRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDocumentProcessingPlanGrain>(
                GrainKeys.DocumentProcessingPlan(orgId, siteId, planId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Plan not found"));

            var snapshot = await grain.ApproveAsync(request.ApprovedBy);

            // Remove from pending queue
            var agentGrain = grainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
                GrainKeys.IngestionAgent(orgId, siteId));
            // We don't need to await a remove - snapshot read is enough to confirm

            return Results.Ok(Hal.Resource(snapshot, BuildPlanLinks(orgId, siteId, planId)));
        });

        // Modify and approve plan
        planGroup.MapPost("/{planId}/modify", async (
            Guid orgId,
            Guid siteId,
            Guid planId,
            [FromBody] ModifyPlanRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDocumentProcessingPlanGrain>(
                GrainKeys.DocumentProcessingPlan(orgId, siteId, planId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Plan not found"));

            var snapshot = await grain.ModifyAndApproveAsync(new ModifyPlanCommand(
                request.ModifiedBy,
                request.DocumentType,
                request.VendorId,
                request.VendorName));

            return Results.Ok(Hal.Resource(snapshot, BuildPlanLinks(orgId, siteId, planId)));
        });

        // Reject plan
        planGroup.MapPost("/{planId}/reject", async (
            Guid orgId,
            Guid siteId,
            Guid planId,
            [FromBody] RejectPlanRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDocumentProcessingPlanGrain>(
                GrainKeys.DocumentProcessingPlan(orgId, siteId, planId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Plan not found"));

            var snapshot = await grain.RejectAsync(request.RejectedBy, request.Reason);
            return Results.Ok(Hal.Resource(snapshot, BuildPlanLinks(orgId, siteId, planId)));
        });

        return app;
    }

    private static Dictionary<string, object> BuildAgentLinks(Guid orgId, Guid siteId)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["activate"] = new { href = $"{basePath}/activate" },
            ["deactivate"] = new { href = $"{basePath}/deactivate" },
            ["poll"] = new { href = $"{basePath}/poll" },
            ["queue"] = new { href = $"{basePath}/queue" },
            ["history"] = new { href = $"{basePath}/history" },
            ["mailboxes"] = new { href = $"{basePath}/mailboxes" },
            ["routing-rules"] = new { href = $"{basePath}/routing-rules" },
            ["email-inbox"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/email-inbox" }
        };
    }

    private static Dictionary<string, object> BuildPlanLinks(Guid orgId, Guid siteId, Guid planId)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent/plans/{planId}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["approve"] = new { href = $"{basePath}/approve" },
            ["modify"] = new { href = $"{basePath}/modify" },
            ["reject"] = new { href = $"{basePath}/reject" },
            ["agent"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/ingestion-agent" }
        };
    }
}
