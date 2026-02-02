namespace DarkVelocity.Host.Events;

// ============================================================================
// Recipe Document Events
// ============================================================================

/// <summary>
/// A new recipe document was created.
/// </summary>
public sealed record RecipeDocumentCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    string Name,
    decimal CostPerPortion,
    Guid? CategoryId
) : IntegrationEvent
{
    public override string EventType => "recipe.document.created";
}

/// <summary>
/// A new draft version was created for a recipe document.
/// </summary>
public sealed record RecipeDraftCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "recipe.draft.created";
}

/// <summary>
/// A recipe draft was published to become the live version.
/// </summary>
public sealed record RecipeDraftPublished(
    string DocumentId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy,
    decimal CostPerPortion
) : IntegrationEvent
{
    public override string EventType => "recipe.draft.published";
}

/// <summary>
/// A recipe was reverted to a previous version.
/// </summary>
public sealed record RecipeDocumentReverted(
    string DocumentId,
    Guid OrgId,
    int FromVersion,
    int ToVersion,
    Guid? RevertedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "recipe.version.reverted";
}

/// <summary>
/// A scheduled change was created for a recipe.
/// </summary>
public sealed record RecipeChangeScheduled(
    string DocumentId,
    Guid OrgId,
    string ScheduleId,
    int Version,
    DateTimeOffset ActivateAt,
    DateTimeOffset? DeactivateAt
) : IntegrationEvent
{
    public override string EventType => "recipe.change.scheduled";
}

/// <summary>
/// A scheduled change was cancelled for a recipe.
/// </summary>
public sealed record RecipeScheduleCancelled(
    string DocumentId,
    Guid OrgId,
    string ScheduleId,
    Guid? CancelledBy
) : IntegrationEvent
{
    public override string EventType => "recipe.schedule.cancelled";
}

/// <summary>
/// A recipe document was archived.
/// </summary>
public sealed record RecipeDocumentArchived(
    string DocumentId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "recipe.document.archived";
}

/// <summary>
/// A recipe document was restored from archive.
/// </summary>
public sealed record RecipeDocumentRestored(
    string DocumentId,
    Guid OrgId,
    Guid? RestoredBy
) : IntegrationEvent
{
    public override string EventType => "recipe.document.restored";
}

/// <summary>
/// A recipe's cost was recalculated.
/// </summary>
public sealed record RecipeCostRecalculated(
    string DocumentId,
    Guid OrgId,
    int Version,
    decimal PreviousCost,
    decimal NewCost,
    decimal CostChange,
    decimal CostChangePercent
) : IntegrationEvent
{
    public override string EventType => "recipe.cost.recalculated";
}

/// <summary>
/// A recipe was linked to a menu item.
/// </summary>
public sealed record RecipeLinkedToMenuItem(
    string RecipeDocumentId,
    string MenuItemDocumentId,
    Guid OrgId
) : IntegrationEvent
{
    public override string EventType => "recipe.menu_item.linked";
}

/// <summary>
/// A recipe was unlinked from a menu item.
/// </summary>
public sealed record RecipeUnlinkedFromMenuItem(
    string RecipeDocumentId,
    string MenuItemDocumentId,
    Guid OrgId
) : IntegrationEvent
{
    public override string EventType => "recipe.menu_item.unlinked";
}

// ============================================================================
// Recipe Category Document Events
// ============================================================================

/// <summary>
/// A new recipe category document was created.
/// </summary>
public sealed record RecipeCategoryDocumentCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    string Name,
    int DisplayOrder
) : IntegrationEvent
{
    public override string EventType => "recipe.category.document.created";
}

/// <summary>
/// A new draft version was created for a recipe category document.
/// </summary>
public sealed record RecipeCategoryDraftCreated(
    string DocumentId,
    Guid OrgId,
    int Version,
    Guid? CreatedBy,
    string? ChangeNote
) : IntegrationEvent
{
    public override string EventType => "recipe.category.draft.created";
}

/// <summary>
/// A recipe category draft was published to become the live version.
/// </summary>
public sealed record RecipeCategoryDraftPublished(
    string DocumentId,
    Guid OrgId,
    int Version,
    int? PreviousPublishedVersion,
    Guid? PublishedBy
) : IntegrationEvent
{
    public override string EventType => "recipe.category.draft.published";
}

/// <summary>
/// A recipe category document was archived.
/// </summary>
public sealed record RecipeCategoryDocumentArchived(
    string DocumentId,
    Guid OrgId,
    Guid? ArchivedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "recipe.category.document.archived";
}

/// <summary>
/// A recipe category document was restored from archive.
/// </summary>
public sealed record RecipeCategoryDocumentRestored(
    string DocumentId,
    Guid OrgId,
    Guid? RestoredBy
) : IntegrationEvent
{
    public override string EventType => "recipe.category.document.restored";
}
