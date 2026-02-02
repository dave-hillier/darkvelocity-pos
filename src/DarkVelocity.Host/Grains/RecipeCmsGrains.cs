using DarkVelocity.Host.Events;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Recipe Document Grain Implementation
// ============================================================================

/// <summary>
/// Grain for recipe document management with versioning and workflow.
/// Provides CMS-like functionality: draft/publish, versioning, scheduling, localization.
/// </summary>
public class RecipeDocumentGrain : Grain, IRecipeDocumentGrain
{
    private readonly IPersistentState<RecipeDocumentState> _state;
    private readonly IGrainFactory _grainFactory;

    public RecipeDocumentGrain(
        [PersistentState("recipeDocument", "OrleansStorage")]
        IPersistentState<RecipeDocumentState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    private async Task RecordHistoryAsync(
        CmsChangeType changeType,
        int fromVersion,
        int toVersion,
        Guid? userId,
        string? changeNote,
        IReadOnlyList<FieldChange> changes)
    {
        var changeEvent = new CmsContentChanged(
            DocumentType: "Recipe",
            DocumentId: _state.State.DocumentId,
            OrgId: _state.State.OrgId,
            FromVersion: fromVersion,
            ToVersion: toVersion,
            ChangedBy: userId,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: changeType,
            Changes: changes,
            ChangeNote: changeNote);

        var historyGrain = _grainFactory.GetGrain<ICmsHistoryGrain>(
            GrainKeys.CmsHistory(_state.State.OrgId, "Recipe", _state.State.DocumentId));
        await historyGrain.RecordChangeAsync(changeEvent);

        var undoGrain = _grainFactory.GetGrain<ICmsUndoGrain>(
            GrainKeys.CmsUndo(_state.State.OrgId, "Recipe", _state.State.DocumentId));
        await undoGrain.PushAsync(changeEvent);
    }

    public async Task<RecipeDocumentSnapshot> CreateAsync(CreateRecipeDocumentCommand command)
    {
        if (_state.State.IsCreated)
            throw new InvalidOperationException("Recipe document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        var ingredients = command.Ingredients?.Select((i, idx) => new RecipeIngredientState
        {
            IngredientId = i.IngredientId,
            IngredientName = i.IngredientName,
            Quantity = i.Quantity,
            Unit = i.Unit,
            WastePercentage = i.WastePercentage,
            UnitCost = i.UnitCost,
            PrepInstructions = i.PrepInstructions,
            IsOptional = i.IsOptional,
            DisplayOrder = i.DisplayOrder == 0 ? idx : i.DisplayOrder,
            SubstitutionIds = i.SubstitutionIds?.ToList() ?? []
        }).ToList() ?? [];

        var version = new RecipeVersionState
        {
            VersionNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = "Initial creation",
            Content = new LocalizedContent
            {
                DefaultLocale = command.Locale,
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    [command.Locale] = new LocalizedStrings
                    {
                        Name = command.Name,
                        Description = command.Description
                    }
                }
            },
            Media = !string.IsNullOrEmpty(command.ImageUrl)
                ? new MediaInfo { PrimaryImageUrl = command.ImageUrl }
                : null,
            PortionYield = command.PortionYield,
            YieldUnit = command.YieldUnit,
            Ingredients = ingredients,
            AllergenTags = command.AllergenTags?.ToList() ?? [],
            DietaryTags = command.DietaryTags?.ToList() ?? [],
            PrepInstructions = command.PrepInstructions,
            PrepTimeMinutes = command.PrepTimeMinutes,
            CookTimeMinutes = command.CookTimeMinutes,
            CategoryId = command.CategoryId
        };

        _state.State = new RecipeDocumentState
        {
            OrgId = orgId,
            DocumentId = documentId,
            IsCreated = true,
            CurrentVersion = 1,
            PublishedVersion = command.PublishImmediately ? 1 : null,
            DraftVersion = command.PublishImmediately ? null : 1,
            Versions = [version],
            CreatedAt = DateTimeOffset.UtcNow,
            AuditLog =
            [
                new AuditEntry
                {
                    Action = "Created",
                    UserId = command.CreatedBy,
                    VersionNumber = 1
                }
            ]
        };

        if (command.PublishImmediately)
        {
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "Published",
                UserId = command.CreatedBy,
                VersionNumber = 1
            });
        }

        await _state.WriteStateAsync();

        // Record history for creation
        var changes = CmsFieldChangeService.ComputeRecipeChanges(null, version);
        var changeType = command.PublishImmediately ? CmsChangeType.Created : CmsChangeType.DraftCreated;
        await RecordHistoryAsync(changeType, 0, 1, command.CreatedBy, "Initial creation", changes);

        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.IsCreated);
    }

    public async Task<RecipeVersionSnapshot> CreateDraftAsync(CreateRecipeDraftCommand command)
    {
        EnsureInitialized();

        // Get base version (either current draft or published)
        var baseVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var ingredients = command.Ingredients?.Select((i, idx) => new RecipeIngredientState
        {
            IngredientId = i.IngredientId,
            IngredientName = i.IngredientName,
            Quantity = i.Quantity,
            Unit = i.Unit,
            WastePercentage = i.WastePercentage,
            UnitCost = i.UnitCost,
            PrepInstructions = i.PrepInstructions,
            IsOptional = i.IsOptional,
            DisplayOrder = i.DisplayOrder == 0 ? idx : i.DisplayOrder,
            SubstitutionIds = i.SubstitutionIds?.ToList() ?? []
        }).ToList() ?? baseVersion.Ingredients.Select(i => new RecipeIngredientState
        {
            IngredientId = i.IngredientId,
            IngredientName = i.IngredientName,
            Quantity = i.Quantity,
            Unit = i.Unit,
            WastePercentage = i.WastePercentage,
            UnitCost = i.UnitCost,
            PrepInstructions = i.PrepInstructions,
            IsOptional = i.IsOptional,
            DisplayOrder = i.DisplayOrder,
            SubstitutionIds = [.. i.SubstitutionIds]
        }).ToList();

        var newVersion = new RecipeVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = command.ChangeNote,
            Content = CloneContent(baseVersion.Content),
            Media = baseVersion.Media != null ? new MediaInfo
            {
                PrimaryImageUrl = command.ImageUrl ?? baseVersion.Media.PrimaryImageUrl,
                ThumbnailUrl = baseVersion.Media.ThumbnailUrl,
                AdditionalImageUrls = [.. baseVersion.Media.AdditionalImageUrls]
            } : command.ImageUrl != null ? new MediaInfo { PrimaryImageUrl = command.ImageUrl } : null,
            PortionYield = command.PortionYield ?? baseVersion.PortionYield,
            YieldUnit = command.YieldUnit ?? baseVersion.YieldUnit,
            Ingredients = ingredients,
            AllergenTags = command.AllergenTags?.ToList() ?? [.. baseVersion.AllergenTags],
            DietaryTags = command.DietaryTags?.ToList() ?? [.. baseVersion.DietaryTags],
            PrepInstructions = command.PrepInstructions ?? baseVersion.PrepInstructions,
            PrepTimeMinutes = command.PrepTimeMinutes ?? baseVersion.PrepTimeMinutes,
            CookTimeMinutes = command.CookTimeMinutes ?? baseVersion.CookTimeMinutes,
            CategoryId = command.CategoryId ?? baseVersion.CategoryId
        };

        // Update name/description if provided
        if (command.Name != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Name = command.Name;
        }
        if (command.Description != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Description = command.Description;
        }

        _state.State.Versions.Add(newVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.DraftVersion = newVersionNumber;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftCreated",
            UserId = command.CreatedBy,
            Note = command.ChangeNote,
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();

        // Record history for draft creation
        var changes = CmsFieldChangeService.ComputeRecipeChanges(baseVersion, newVersion);
        await RecordHistoryAsync(CmsChangeType.DraftCreated, baseVersion.VersionNumber, newVersionNumber,
            command.CreatedBy, command.ChangeNote, changes);

        return ToVersionSnapshot(newVersion);
    }

    public Task<RecipeVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<RecipeVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!_state.State.PublishedVersion.HasValue)
            return Task.FromResult<RecipeVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
        return Task.FromResult<RecipeVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<RecipeVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!_state.State.DraftVersion.HasValue)
            return Task.FromResult<RecipeVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
        return Task.FromResult<RecipeVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<RecipeVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = _state.State.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .Select(ToVersionSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<RecipeVersionSnapshot>>(versions);
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        var draftVersion = _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value);
        var previousVersion = _state.State.PublishedVersion.HasValue
            ? _state.State.Versions.FirstOrDefault(v => v.VersionNumber == _state.State.PublishedVersion.Value)
            : null;
        var previousPublished = _state.State.PublishedVersion;

        _state.State.PublishedVersion = _state.State.DraftVersion;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Published",
            UserId = publishedBy,
            Note = note ?? $"Published version {_state.State.PublishedVersion} (previously {previousPublished})",
            VersionNumber = _state.State.PublishedVersion
        });

        await _state.WriteStateAsync();

        // Record history for publish and notify undo grain
        var changes = CmsFieldChangeService.ComputeRecipeChanges(previousVersion, draftVersion);
        await RecordHistoryAsync(CmsChangeType.Published, previousPublished ?? 0, draftVersion.VersionNumber,
            publishedBy, note ?? $"Published version {draftVersion.VersionNumber}", changes);

        var undoGrain = _grainFactory.GetGrain<ICmsUndoGrain>(
            GrainKeys.CmsUndo(_state.State.OrgId, "Recipe", _state.State.DocumentId));
        await undoGrain.MarkPublishedAsync(draftVersion.VersionNumber);
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            return;

        var discardedDraftVersion = _state.State.DraftVersion.Value;
        _state.State.Versions.RemoveAll(v => v.VersionNumber == discardedDraftVersion);
        _state.State.DraftVersion = null;

        // Recalculate current version
        _state.State.CurrentVersion = _state.State.Versions.Max(v => v.VersionNumber);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftDiscarded",
            VersionNumber = discardedDraftVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null)
    {
        EnsureInitialized();

        var targetVersion = _state.State.Versions.FirstOrDefault(v => v.VersionNumber == version)
            ?? throw new InvalidOperationException($"Version {version} not found");

        var previousVersion = _state.State.PublishedVersion.HasValue
            ? _state.State.Versions.FirstOrDefault(v => v.VersionNumber == _state.State.PublishedVersion.Value)
            : null;
        var previousPublished = _state.State.PublishedVersion;
        var newVersionNumber = _state.State.CurrentVersion + 1;

        // Create a new version that's a copy of the target version
        var revertedVersion = new RecipeVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = revertedBy,
            ChangeNote = reason ?? $"Reverted from version {_state.State.PublishedVersion} to version {version}",
            Content = CloneContent(targetVersion.Content),
            Media = targetVersion.Media,
            PortionYield = targetVersion.PortionYield,
            YieldUnit = targetVersion.YieldUnit,
            Ingredients = targetVersion.Ingredients.Select(i => new RecipeIngredientState
            {
                IngredientId = i.IngredientId,
                IngredientName = i.IngredientName,
                Quantity = i.Quantity,
                Unit = i.Unit,
                WastePercentage = i.WastePercentage,
                UnitCost = i.UnitCost,
                PrepInstructions = i.PrepInstructions,
                IsOptional = i.IsOptional,
                DisplayOrder = i.DisplayOrder,
                SubstitutionIds = [.. i.SubstitutionIds]
            }).ToList(),
            AllergenTags = [.. targetVersion.AllergenTags],
            DietaryTags = [.. targetVersion.DietaryTags],
            PrepInstructions = targetVersion.PrepInstructions,
            PrepTimeMinutes = targetVersion.PrepTimeMinutes,
            CookTimeMinutes = targetVersion.CookTimeMinutes,
            CategoryId = targetVersion.CategoryId
        };

        _state.State.Versions.Add(revertedVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.PublishedVersion = newVersionNumber;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Reverted",
            UserId = revertedBy,
            Note = reason ?? $"Reverted to version {version}",
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();

        // Record history for revert
        var changes = CmsFieldChangeService.ComputeRecipeChanges(previousVersion, revertedVersion);
        await RecordHistoryAsync(CmsChangeType.Reverted, previousPublished ?? 0, newVersionNumber,
            revertedBy, reason ?? $"Reverted to version {version}", changes);
    }

    public async Task AddTranslationAsync(AddRecipeTranslationCommand command)
    {
        EnsureInitialized();

        // Add to draft if exists, otherwise to published
        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        targetVersion.Content.Translations[command.Locale] = new LocalizedStrings
        {
            Name = command.Name,
            Description = command.Description
        };

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = $"TranslationAdded:{command.Locale}",
            VersionNumber = targetVersion.VersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task RemoveTranslationAsync(string locale)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        if (locale == targetVersion.Content.DefaultLocale)
            throw new InvalidOperationException("Cannot remove default locale translation");

        targetVersion.Content.Translations.Remove(locale);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = $"TranslationRemoved:{locale}",
            VersionNumber = targetVersion.VersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!_state.State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var schedule = new ScheduledChange
        {
            ScheduleId = Guid.NewGuid().ToString(),
            VersionToActivate = version,
            ActivateAt = activateAt,
            DeactivateAt = deactivateAt,
            Name = name,
            IsActive = true
        };

        _state.State.Schedules.Add(schedule);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ScheduleCreated",
            Note = $"Scheduled version {version} for {activateAt}",
            VersionNumber = version
        });

        await _state.WriteStateAsync();
        return schedule;
    }

    public async Task CancelScheduleAsync(string scheduleId)
    {
        EnsureInitialized();

        var schedule = _state.State.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (schedule != null)
        {
            _state.State.Schedules.Remove(schedule);

            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "ScheduleCancelled",
                Note = $"Cancelled schedule {scheduleId}"
            });

            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<ScheduledChange>> GetSchedulesAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<ScheduledChange>>(_state.State.Schedules.Where(s => s.IsActive).ToList());
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = true;
        _state.State.ArchivedAt = DateTimeOffset.UtcNow;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Archived",
            UserId = archivedBy,
            Note = reason
        });

        await _state.WriteStateAsync();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = false;
        _state.State.ArchivedAt = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Restored",
            UserId = restoredBy
        });

        await _state.WriteStateAsync();
    }

    public async Task RecalculateCostAsync(IReadOnlyDictionary<Guid, decimal>? ingredientPrices = null)
    {
        EnsureInitialized();

        // Update costs in the draft or published version
        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        if (ingredientPrices != null)
        {
            foreach (var ingredient in targetVersion.Ingredients)
            {
                if (ingredientPrices.TryGetValue(ingredient.IngredientId, out var newPrice))
                {
                    ingredient.UnitCost = newPrice;
                }
            }
        }

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "CostRecalculated",
            Note = $"Theoretical cost: {targetVersion.TheoreticalCost:C}, Cost per portion: {targetVersion.CostPerPortion:C}",
            VersionNumber = targetVersion.VersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task LinkMenuItemAsync(string menuItemDocumentId)
    {
        EnsureInitialized();

        if (!_state.State.LinkedMenuItemIds.Contains(menuItemDocumentId))
        {
            _state.State.LinkedMenuItemIds.Add(menuItemDocumentId);

            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "MenuItemLinked",
                Note = $"Linked to menu item {menuItemDocumentId}"
            });

            await _state.WriteStateAsync();
        }
    }

    public async Task UnlinkMenuItemAsync(string menuItemDocumentId)
    {
        EnsureInitialized();

        if (_state.State.LinkedMenuItemIds.Remove(menuItemDocumentId))
        {
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "MenuItemUnlinked",
                Note = $"Unlinked from menu item {menuItemDocumentId}"
            });

            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<string>> GetLinkedMenuItemsAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<string>>(_state.State.LinkedMenuItemIds);
    }

    public Task<RecipeDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    public Task<RecipeVersionSnapshot?> PreviewAtAsync(DateTimeOffset when)
    {
        EnsureInitialized();

        // Check schedules for what would be active at that time
        var activeSchedule = _state.State.Schedules
            .Where(s => s.IsActive && s.ActivateAt <= when)
            .Where(s => !s.DeactivateAt.HasValue || s.DeactivateAt.Value > when)
            .OrderByDescending(s => s.ActivateAt)
            .FirstOrDefault();

        if (activeSchedule != null)
        {
            var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == activeSchedule.VersionToActivate);
            return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
        }

        // Return published version
        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            return Task.FromResult<RecipeVersionSnapshot?>(ToVersionSnapshot(v));
        }

        return Task.FromResult<RecipeVersionSnapshot?>(null);
    }

    private RecipeDocumentSnapshot GetSnapshot()
    {
        RecipeVersionSnapshot? published = null;
        RecipeVersionSnapshot? draft = null;

        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (_state.State.DraftVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new RecipeDocumentSnapshot(
            DocumentId: _state.State.DocumentId,
            OrgId: _state.State.OrgId,
            CurrentVersion: _state.State.CurrentVersion,
            PublishedVersion: _state.State.PublishedVersion,
            DraftVersion: _state.State.DraftVersion,
            IsArchived: _state.State.IsArchived,
            CreatedAt: _state.State.CreatedAt,
            Published: published,
            Draft: draft,
            Schedules: _state.State.Schedules.Where(s => s.IsActive).ToList(),
            TotalVersions: _state.State.Versions.Count,
            LinkedMenuItemIds: _state.State.LinkedMenuItemIds);
    }

    private static RecipeVersionSnapshot ToVersionSnapshot(RecipeVersionState v)
    {
        var defaultStrings = v.Content.GetStrings();
        return new RecipeVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: defaultStrings.Name,
            Description: defaultStrings.Description,
            PortionYield: v.PortionYield,
            YieldUnit: v.YieldUnit,
            Ingredients: v.Ingredients.Select(i => new RecipeIngredientSnapshot(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Quantity: i.Quantity,
                Unit: i.Unit,
                WastePercentage: i.WastePercentage,
                EffectiveQuantity: i.EffectiveQuantity,
                UnitCost: i.UnitCost,
                LineCost: i.LineCost,
                PrepInstructions: i.PrepInstructions,
                IsOptional: i.IsOptional,
                DisplayOrder: i.DisplayOrder,
                SubstitutionIds: i.SubstitutionIds
            )).ToList(),
            AllergenTags: v.AllergenTags,
            DietaryTags: v.DietaryTags,
            PrepInstructions: v.PrepInstructions,
            PrepTimeMinutes: v.PrepTimeMinutes,
            CookTimeMinutes: v.CookTimeMinutes,
            ImageUrl: v.Media?.PrimaryImageUrl,
            CategoryId: v.CategoryId,
            TheoreticalCost: v.TheoreticalCost,
            CostPerPortion: v.CostPerPortion,
            Translations: v.Content.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
    }

    private static LocalizedContent CloneContent(LocalizedContent source)
    {
        return new LocalizedContent
        {
            DefaultLocale = source.DefaultLocale,
            Translations = source.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => new LocalizedStrings
                {
                    Name = kvp.Value.Name,
                    Description = kvp.Value.Description,
                    KitchenName = kvp.Value.KitchenName
                })
        };
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsCreated)
            throw new InvalidOperationException("Recipe document not initialized");
    }
}

// ============================================================================
// Recipe Category Document Grain Implementation
// ============================================================================

/// <summary>
/// Grain for recipe category document management with versioning and workflow.
/// </summary>
public class RecipeCategoryDocumentGrain : Grain, IRecipeCategoryDocumentGrain
{
    private readonly IPersistentState<RecipeCategoryDocumentState> _state;

    public RecipeCategoryDocumentGrain(
        [PersistentState("recipeCategoryDocument", "OrleansStorage")]
        IPersistentState<RecipeCategoryDocumentState> state)
    {
        _state = state;
    }

    public async Task<RecipeCategoryDocumentSnapshot> CreateAsync(CreateRecipeCategoryDocumentCommand command)
    {
        if (_state.State.IsCreated)
            throw new InvalidOperationException("Recipe category document already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var documentId = parts[2];

        var version = new RecipeCategoryVersionState
        {
            VersionNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = "Initial creation",
            Content = new LocalizedContent
            {
                DefaultLocale = command.Locale,
                Translations = new Dictionary<string, LocalizedStrings>
                {
                    [command.Locale] = new LocalizedStrings
                    {
                        Name = command.Name,
                        Description = command.Description
                    }
                }
            },
            Color = command.Color,
            IconUrl = command.IconUrl,
            DisplayOrder = command.DisplayOrder
        };

        _state.State = new RecipeCategoryDocumentState
        {
            OrgId = orgId,
            DocumentId = documentId,
            IsCreated = true,
            CurrentVersion = 1,
            PublishedVersion = command.PublishImmediately ? 1 : null,
            DraftVersion = command.PublishImmediately ? null : 1,
            Versions = [version],
            CreatedAt = DateTimeOffset.UtcNow,
            AuditLog =
            [
                new AuditEntry
                {
                    Action = "Created",
                    UserId = command.CreatedBy,
                    VersionNumber = 1
                }
            ]
        };

        if (command.PublishImmediately)
        {
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "Published",
                UserId = command.CreatedBy,
                VersionNumber = 1
            });
        }

        await _state.WriteStateAsync();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.IsCreated);
    }

    public async Task<RecipeCategoryVersionSnapshot> CreateDraftAsync(CreateRecipeCategoryDraftCommand command)
    {
        EnsureInitialized();

        var baseVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var newVersion = new RecipeCategoryVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = command.CreatedBy,
            ChangeNote = command.ChangeNote,
            Content = CloneContent(baseVersion.Content),
            Color = command.Color ?? baseVersion.Color,
            IconUrl = command.IconUrl ?? baseVersion.IconUrl,
            DisplayOrder = command.DisplayOrder ?? baseVersion.DisplayOrder,
            RecipeDocumentIds = command.RecipeDocumentIds?.ToList() ?? [.. baseVersion.RecipeDocumentIds]
        };

        if (command.Name != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Name = command.Name;
        }
        if (command.Description != null)
        {
            var defaultLocale = newVersion.Content.DefaultLocale;
            newVersion.Content.Translations[defaultLocale].Description = command.Description;
        }

        _state.State.Versions.Add(newVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.DraftVersion = newVersionNumber;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftCreated",
            UserId = command.CreatedBy,
            Note = command.ChangeNote,
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
        return ToVersionSnapshot(newVersion);
    }

    public Task<RecipeCategoryVersionSnapshot?> GetVersionAsync(int version)
    {
        EnsureInitialized();
        var v = _state.State.Versions.FirstOrDefault(x => x.VersionNumber == version);
        return Task.FromResult(v != null ? ToVersionSnapshot(v) : null);
    }

    public Task<RecipeCategoryVersionSnapshot?> GetPublishedAsync()
    {
        EnsureInitialized();
        if (!_state.State.PublishedVersion.HasValue)
            return Task.FromResult<RecipeCategoryVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
        return Task.FromResult<RecipeCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<RecipeCategoryVersionSnapshot?> GetDraftAsync()
    {
        EnsureInitialized();
        if (!_state.State.DraftVersion.HasValue)
            return Task.FromResult<RecipeCategoryVersionSnapshot?>(null);

        var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
        return Task.FromResult<RecipeCategoryVersionSnapshot?>(ToVersionSnapshot(v));
    }

    public Task<IReadOnlyList<RecipeCategoryVersionSnapshot>> GetVersionHistoryAsync(int skip = 0, int take = 20)
    {
        EnsureInitialized();
        var versions = _state.State.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .Select(ToVersionSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<RecipeCategoryVersionSnapshot>>(versions);
    }

    public async Task PublishDraftAsync(Guid? publishedBy = null, string? note = null)
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            throw new InvalidOperationException("No draft to publish");

        var previousPublished = _state.State.PublishedVersion;
        _state.State.PublishedVersion = _state.State.DraftVersion;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Published",
            UserId = publishedBy,
            Note = note ?? $"Published version {_state.State.PublishedVersion} (previously {previousPublished})",
            VersionNumber = _state.State.PublishedVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task DiscardDraftAsync()
    {
        EnsureInitialized();

        if (!_state.State.DraftVersion.HasValue)
            return;

        var draftVersion = _state.State.DraftVersion.Value;
        _state.State.Versions.RemoveAll(v => v.VersionNumber == draftVersion);
        _state.State.DraftVersion = null;
        _state.State.CurrentVersion = _state.State.Versions.Max(v => v.VersionNumber);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "DraftDiscarded",
            VersionNumber = draftVersion
        });

        await _state.WriteStateAsync();
    }

    public async Task RevertToVersionAsync(int version, Guid? revertedBy = null, string? reason = null)
    {
        EnsureInitialized();

        var targetVersion = _state.State.Versions.FirstOrDefault(v => v.VersionNumber == version)
            ?? throw new InvalidOperationException($"Version {version} not found");

        var newVersionNumber = _state.State.CurrentVersion + 1;

        var revertedVersion = new RecipeCategoryVersionState
        {
            VersionNumber = newVersionNumber,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = revertedBy,
            ChangeNote = reason ?? $"Reverted from version {_state.State.PublishedVersion} to version {version}",
            Content = CloneContent(targetVersion.Content),
            Color = targetVersion.Color,
            IconUrl = targetVersion.IconUrl,
            DisplayOrder = targetVersion.DisplayOrder,
            RecipeDocumentIds = [.. targetVersion.RecipeDocumentIds]
        };

        _state.State.Versions.Add(revertedVersion);
        _state.State.CurrentVersion = newVersionNumber;
        _state.State.PublishedVersion = newVersionNumber;
        _state.State.DraftVersion = null;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Reverted",
            UserId = revertedBy,
            Note = reason ?? $"Reverted to version {version}",
            VersionNumber = newVersionNumber
        });

        await _state.WriteStateAsync();
    }

    public async Task AddRecipeAsync(string recipeDocumentId)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        if (!targetVersion.RecipeDocumentIds.Contains(recipeDocumentId))
        {
            targetVersion.RecipeDocumentIds.Add(recipeDocumentId);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveRecipeAsync(string recipeDocumentId)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        targetVersion.RecipeDocumentIds.Remove(recipeDocumentId);
        await _state.WriteStateAsync();
    }

    public async Task ReorderRecipesAsync(IReadOnlyList<string> recipeDocumentIds)
    {
        EnsureInitialized();

        var targetVersion = _state.State.DraftVersion.HasValue
            ? _state.State.Versions.First(v => v.VersionNumber == _state.State.DraftVersion.Value)
            : _state.State.Versions.First(v => v.VersionNumber == _state.State.PublishedVersion);

        targetVersion.RecipeDocumentIds = recipeDocumentIds.ToList();
        await _state.WriteStateAsync();
    }

    public async Task<ScheduledChange> ScheduleChangeAsync(int version, DateTimeOffset activateAt, DateTimeOffset? deactivateAt = null, string? name = null)
    {
        EnsureInitialized();

        if (!_state.State.Versions.Any(v => v.VersionNumber == version))
            throw new InvalidOperationException($"Version {version} not found");

        var schedule = new ScheduledChange
        {
            ScheduleId = Guid.NewGuid().ToString(),
            VersionToActivate = version,
            ActivateAt = activateAt,
            DeactivateAt = deactivateAt,
            Name = name,
            IsActive = true
        };

        _state.State.Schedules.Add(schedule);

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "ScheduleCreated",
            Note = $"Scheduled version {version} for {activateAt}",
            VersionNumber = version
        });

        await _state.WriteStateAsync();
        return schedule;
    }

    public async Task CancelScheduleAsync(string scheduleId)
    {
        EnsureInitialized();

        var schedule = _state.State.Schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
        if (schedule != null)
        {
            _state.State.Schedules.Remove(schedule);
            _state.State.AuditLog.Add(new AuditEntry
            {
                Action = "ScheduleCancelled",
                Note = $"Cancelled schedule {scheduleId}"
            });
            await _state.WriteStateAsync();
        }
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = true;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Archived",
            UserId = archivedBy,
            Note = reason
        });

        await _state.WriteStateAsync();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        _state.State.IsArchived = false;

        _state.State.AuditLog.Add(new AuditEntry
        {
            Action = "Restored",
            UserId = restoredBy
        });

        await _state.WriteStateAsync();
    }

    public Task<RecipeCategoryDocumentSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    private RecipeCategoryDocumentSnapshot GetSnapshot()
    {
        RecipeCategoryVersionSnapshot? published = null;
        RecipeCategoryVersionSnapshot? draft = null;

        if (_state.State.PublishedVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.PublishedVersion.Value);
            published = ToVersionSnapshot(v);
        }

        if (_state.State.DraftVersion.HasValue)
        {
            var v = _state.State.Versions.First(x => x.VersionNumber == _state.State.DraftVersion.Value);
            draft = ToVersionSnapshot(v);
        }

        return new RecipeCategoryDocumentSnapshot(
            DocumentId: _state.State.DocumentId,
            OrgId: _state.State.OrgId,
            CurrentVersion: _state.State.CurrentVersion,
            PublishedVersion: _state.State.PublishedVersion,
            DraftVersion: _state.State.DraftVersion,
            IsArchived: _state.State.IsArchived,
            CreatedAt: _state.State.CreatedAt,
            Published: published,
            Draft: draft,
            Schedules: _state.State.Schedules.Where(s => s.IsActive).ToList(),
            TotalVersions: _state.State.Versions.Count);
    }

    private static RecipeCategoryVersionSnapshot ToVersionSnapshot(RecipeCategoryVersionState v)
    {
        var defaultStrings = v.Content.GetStrings();
        return new RecipeCategoryVersionSnapshot(
            VersionNumber: v.VersionNumber,
            CreatedAt: v.CreatedAt,
            CreatedBy: v.CreatedBy,
            ChangeNote: v.ChangeNote,
            Name: defaultStrings.Name,
            Description: defaultStrings.Description,
            Color: v.Color,
            IconUrl: v.IconUrl,
            DisplayOrder: v.DisplayOrder,
            RecipeDocumentIds: v.RecipeDocumentIds,
            Translations: v.Content.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value));
    }

    private static LocalizedContent CloneContent(LocalizedContent source)
    {
        return new LocalizedContent
        {
            DefaultLocale = source.DefaultLocale,
            Translations = source.Translations.ToDictionary(
                kvp => kvp.Key,
                kvp => new LocalizedStrings
                {
                    Name = kvp.Value.Name,
                    Description = kvp.Value.Description,
                    KitchenName = kvp.Value.KitchenName
                })
        };
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsCreated)
            throw new InvalidOperationException("Recipe category document not initialized");
    }
}

// ============================================================================
// Recipe Registry Grain Implementation
// ============================================================================

/// <summary>
/// Grain for maintaining a registry of recipe documents.
/// </summary>
public class RecipeRegistryGrain : Grain, IRecipeRegistryGrain
{
    private readonly IPersistentState<RecipeRegistryState> _state;

    public RecipeRegistryGrain(
        [PersistentState("recipeRegistry", "OrleansStorage")]
        IPersistentState<RecipeRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterRecipeAsync(string documentId, string name, decimal costPerPortion, string? categoryId, int linkedMenuItemCount = 0)
    {
        await EnsureInitializedAsync();

        _state.State.Recipes[documentId] = new RecipeRegistryEntry
        {
            DocumentId = documentId,
            Name = name,
            CostPerPortion = costPerPortion,
            CategoryId = categoryId,
            HasDraft = false,
            IsArchived = false,
            PublishedVersion = 1,
            LastModified = DateTimeOffset.UtcNow,
            LinkedMenuItemCount = linkedMenuItemCount
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateRecipeAsync(string documentId, string name, decimal costPerPortion, string? categoryId, bool hasDraft, bool isArchived, int linkedMenuItemCount)
    {
        await EnsureInitializedAsync();

        if (_state.State.Recipes.TryGetValue(documentId, out var entry))
        {
            entry.Name = name;
            entry.CostPerPortion = costPerPortion;
            entry.CategoryId = categoryId;
            entry.HasDraft = hasDraft;
            entry.IsArchived = isArchived;
            entry.LastModified = DateTimeOffset.UtcNow;
            entry.LinkedMenuItemCount = linkedMenuItemCount;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterRecipeAsync(string documentId)
    {
        await EnsureInitializedAsync();
        _state.State.Recipes.Remove(documentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<RecipeDocumentSummary>> GetRecipesAsync(string? categoryId = null, bool includeArchived = false)
    {
        var recipes = _state.State.Recipes.Values
            .Where(r => includeArchived || !r.IsArchived)
            .Where(r => categoryId == null || r.CategoryId == categoryId)
            .OrderBy(r => r.Name)
            .Select(r => new RecipeDocumentSummary(
                DocumentId: r.DocumentId,
                Name: r.Name,
                CostPerPortion: r.CostPerPortion,
                CategoryId: r.CategoryId,
                HasDraft: r.HasDraft,
                IsArchived: r.IsArchived,
                PublishedVersion: r.PublishedVersion,
                LastModified: r.LastModified,
                LinkedMenuItemCount: r.LinkedMenuItemCount))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeDocumentSummary>>(recipes);
    }

    public async Task RegisterCategoryAsync(string documentId, string name, int displayOrder, string? color)
    {
        await EnsureInitializedAsync();

        _state.State.Categories[documentId] = new RecipeCategoryRegistryEntry
        {
            DocumentId = documentId,
            Name = name,
            DisplayOrder = displayOrder,
            Color = color,
            HasDraft = false,
            IsArchived = false,
            RecipeCount = 0,
            LastModified = DateTimeOffset.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateCategoryAsync(string documentId, string name, int displayOrder, string? color, bool hasDraft, bool isArchived, int recipeCount)
    {
        await EnsureInitializedAsync();

        if (_state.State.Categories.TryGetValue(documentId, out var entry))
        {
            entry.Name = name;
            entry.DisplayOrder = displayOrder;
            entry.Color = color;
            entry.HasDraft = hasDraft;
            entry.IsArchived = isArchived;
            entry.RecipeCount = recipeCount;
            entry.LastModified = DateTimeOffset.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterCategoryAsync(string documentId)
    {
        await EnsureInitializedAsync();
        _state.State.Categories.Remove(documentId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<RecipeCategoryDocumentSummary>> GetCategoriesAsync(bool includeArchived = false)
    {
        var categories = _state.State.Categories.Values
            .Where(c => includeArchived || !c.IsArchived)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new RecipeCategoryDocumentSummary(
                DocumentId: c.DocumentId,
                Name: c.Name,
                DisplayOrder: c.DisplayOrder,
                Color: c.Color,
                HasDraft: c.HasDraft,
                IsArchived: c.IsArchived,
                RecipeCount: c.RecipeCount,
                LastModified: c.LastModified))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeCategoryDocumentSummary>>(categories);
    }

    public Task<IReadOnlyList<RecipeDocumentSummary>> SearchRecipesAsync(string query, int take = 20)
    {
        var lowerQuery = query.ToLowerInvariant();
        var recipes = _state.State.Recipes.Values
            .Where(r => !r.IsArchived)
            .Where(r => r.Name.ToLowerInvariant().Contains(lowerQuery))
            .Take(take)
            .Select(r => new RecipeDocumentSummary(
                DocumentId: r.DocumentId,
                Name: r.Name,
                CostPerPortion: r.CostPerPortion,
                CategoryId: r.CategoryId,
                HasDraft: r.HasDraft,
                IsArchived: r.IsArchived,
                PublishedVersion: r.PublishedVersion,
                LastModified: r.LastModified,
                LinkedMenuItemCount: r.LinkedMenuItemCount))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeDocumentSummary>>(recipes);
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_state.State.IsCreated)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            _state.State.OrgId = Guid.Parse(parts[0]);
            _state.State.IsCreated = true;
            await _state.WriteStateAsync();
        }
    }
}
