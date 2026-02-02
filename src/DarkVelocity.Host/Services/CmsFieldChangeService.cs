using System.Text.Json;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Services;

// ============================================================================
// CMS Field Change Service - Computes field-level diffs between versions
// ============================================================================

/// <summary>
/// Service for computing field-level changes between CMS document versions.
/// </summary>
public static class CmsFieldChangeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Computes the field changes between two menu item versions.
    /// </summary>
    public static IReadOnlyList<FieldChange> ComputeMenuItemChanges(
        MenuItemVersionState? oldVersion,
        MenuItemVersionState newVersion)
    {
        var changes = new List<FieldChange>();

        if (oldVersion == null)
        {
            // Creation - all fields are new
            AddSetChange(changes, "Name", null, GetDefaultName(newVersion.Content));
            AddSetChange(changes, "Description", null, GetDefaultDescription(newVersion.Content));
            AddSetChange(changes, "Price", null, newVersion.Pricing.BasePrice);
            AddSetChange(changes, "CategoryId", null, newVersion.CategoryId);
            AddSetChange(changes, "AccountingGroupId", null, newVersion.AccountingGroupId);
            AddSetChange(changes, "RecipeId", null, newVersion.RecipeId);
            AddSetChange(changes, "ImageUrl", null, newVersion.Media?.PrimaryImageUrl);
            AddSetChange(changes, "Sku", null, newVersion.Sku);
            AddSetChange(changes, "TrackInventory", null, newVersion.TrackInventory);
            AddSetChange(changes, "ModifierBlockIds", null, newVersion.ModifierBlockIds);
            AddSetChange(changes, "TagIds", null, newVersion.TagIds);
            return changes;
        }

        // Compare each field
        CompareField(changes, "Name", GetDefaultName(oldVersion.Content), GetDefaultName(newVersion.Content));
        CompareField(changes, "Description", GetDefaultDescription(oldVersion.Content), GetDefaultDescription(newVersion.Content));
        CompareField(changes, "Price", oldVersion.Pricing.BasePrice, newVersion.Pricing.BasePrice);
        CompareField(changes, "CategoryId", oldVersion.CategoryId, newVersion.CategoryId);
        CompareField(changes, "AccountingGroupId", oldVersion.AccountingGroupId, newVersion.AccountingGroupId);
        CompareField(changes, "RecipeId", oldVersion.RecipeId, newVersion.RecipeId);
        CompareField(changes, "ImageUrl", oldVersion.Media?.PrimaryImageUrl, newVersion.Media?.PrimaryImageUrl);
        CompareField(changes, "Sku", oldVersion.Sku, newVersion.Sku);
        CompareField(changes, "TrackInventory", oldVersion.TrackInventory, newVersion.TrackInventory);
        CompareField(changes, "DisplayOrder", oldVersion.DisplayOrder, newVersion.DisplayOrder);

        // Compare collections
        CompareCollection(changes, "ModifierBlockIds", oldVersion.ModifierBlockIds, newVersion.ModifierBlockIds);
        CompareCollection(changes, "TagIds", oldVersion.TagIds, newVersion.TagIds);

        // Compare translations
        CompareTranslations(changes, "Translations", oldVersion.Content.Translations, newVersion.Content.Translations);

        return changes;
    }

    /// <summary>
    /// Computes the field changes between two menu category versions.
    /// </summary>
    public static IReadOnlyList<FieldChange> ComputeMenuCategoryChanges(
        MenuCategoryVersionState? oldVersion,
        MenuCategoryVersionState newVersion)
    {
        var changes = new List<FieldChange>();

        if (oldVersion == null)
        {
            // Creation
            AddSetChange(changes, "Name", null, GetDefaultName(newVersion.Content));
            AddSetChange(changes, "Description", null, GetDefaultDescription(newVersion.Content));
            AddSetChange(changes, "Color", null, newVersion.Color);
            AddSetChange(changes, "IconUrl", null, newVersion.IconUrl);
            AddSetChange(changes, "DisplayOrder", null, newVersion.DisplayOrder);
            AddSetChange(changes, "ItemDocumentIds", null, newVersion.ItemDocumentIds);
            return changes;
        }

        CompareField(changes, "Name", GetDefaultName(oldVersion.Content), GetDefaultName(newVersion.Content));
        CompareField(changes, "Description", GetDefaultDescription(oldVersion.Content), GetDefaultDescription(newVersion.Content));
        CompareField(changes, "Color", oldVersion.Color, newVersion.Color);
        CompareField(changes, "IconUrl", oldVersion.IconUrl, newVersion.IconUrl);
        CompareField(changes, "DisplayOrder", oldVersion.DisplayOrder, newVersion.DisplayOrder);
        CompareCollection(changes, "ItemDocumentIds", oldVersion.ItemDocumentIds, newVersion.ItemDocumentIds);
        CompareTranslations(changes, "Translations", oldVersion.Content.Translations, newVersion.Content.Translations);

        return changes;
    }

    /// <summary>
    /// Computes the field changes between two modifier block versions.
    /// </summary>
    public static IReadOnlyList<FieldChange> ComputeModifierBlockChanges(
        ModifierBlockVersionState? oldVersion,
        ModifierBlockVersionState newVersion)
    {
        var changes = new List<FieldChange>();

        if (oldVersion == null)
        {
            // Creation
            AddSetChange(changes, "Name", null, GetDefaultName(newVersion.Content));
            AddSetChange(changes, "SelectionRule", null, newVersion.SelectionRule);
            AddSetChange(changes, "MinSelections", null, newVersion.MinSelections);
            AddSetChange(changes, "MaxSelections", null, newVersion.MaxSelections);
            AddSetChange(changes, "IsRequired", null, newVersion.IsRequired);
            AddSetChange(changes, "Options", null, newVersion.Options.Select(o => new { o.OptionId, Name = GetDefaultName(o.Content), o.PriceAdjustment }).ToList());
            return changes;
        }

        CompareField(changes, "Name", GetDefaultName(oldVersion.Content), GetDefaultName(newVersion.Content));
        CompareField(changes, "SelectionRule", oldVersion.SelectionRule, newVersion.SelectionRule);
        CompareField(changes, "MinSelections", oldVersion.MinSelections, newVersion.MinSelections);
        CompareField(changes, "MaxSelections", oldVersion.MaxSelections, newVersion.MaxSelections);
        CompareField(changes, "IsRequired", oldVersion.IsRequired, newVersion.IsRequired);

        // Compare options
        CompareOptions(changes, oldVersion.Options, newVersion.Options);

        return changes;
    }

    private static string? GetDefaultName(LocalizedContent content)
    {
        return content.GetStrings().Name;
    }

    private static string? GetDefaultDescription(LocalizedContent content)
    {
        return content.GetStrings().Description;
    }

    private static void AddSetChange<T>(List<FieldChange> changes, string fieldPath, T? oldValue, T? newValue)
    {
        var oldJson = oldValue != null ? JsonSerializer.Serialize(oldValue, JsonOptions) : null;
        var newJson = newValue != null ? JsonSerializer.Serialize(newValue, JsonOptions) : null;
        changes.Add(FieldChange.Set(fieldPath, oldJson, newJson));
    }

    private static void CompareField<T>(List<FieldChange> changes, string fieldPath, T? oldValue, T? newValue)
    {
        var oldJson = oldValue != null ? JsonSerializer.Serialize(oldValue, JsonOptions) : null;
        var newJson = newValue != null ? JsonSerializer.Serialize(newValue, JsonOptions) : null;

        if (oldJson != newJson)
        {
            changes.Add(FieldChange.Set(fieldPath, oldJson, newJson));
        }
    }

    private static void CompareCollection<T>(List<FieldChange> changes, string fieldPath, IReadOnlyList<T>? oldList, IReadOnlyList<T>? newList)
    {
        oldList ??= [];
        newList ??= [];

        var oldSet = new HashSet<string>(oldList.Select(x => JsonSerializer.Serialize(x, JsonOptions)));
        var newSet = new HashSet<string>(newList.Select(x => JsonSerializer.Serialize(x, JsonOptions)));

        // Find added items
        foreach (var item in newSet.Except(oldSet))
        {
            changes.Add(FieldChange.Add($"{fieldPath}[]", item));
        }

        // Find removed items
        foreach (var item in oldSet.Except(newSet))
        {
            changes.Add(FieldChange.Remove($"{fieldPath}[]", item));
        }

        // Check for reordering if same items
        if (oldSet.SetEquals(newSet) && oldList.Count > 0)
        {
            var oldOrder = JsonSerializer.Serialize(oldList, JsonOptions);
            var newOrder = JsonSerializer.Serialize(newList, JsonOptions);
            if (oldOrder != newOrder)
            {
                changes.Add(FieldChange.Reorder(fieldPath, oldOrder, newOrder));
            }
        }
    }

    private static void CompareTranslations(
        List<FieldChange> changes,
        string basePath,
        Dictionary<string, LocalizedStrings> oldTranslations,
        Dictionary<string, LocalizedStrings> newTranslations)
    {
        var allLocales = oldTranslations.Keys.Union(newTranslations.Keys);

        foreach (var locale in allLocales)
        {
            var hasOld = oldTranslations.TryGetValue(locale, out var oldStrings);
            var hasNew = newTranslations.TryGetValue(locale, out var newStrings);

            if (!hasOld && hasNew)
            {
                // Added locale
                changes.Add(FieldChange.Add($"{basePath}.{locale}",
                    JsonSerializer.Serialize(newStrings, JsonOptions)));
            }
            else if (hasOld && !hasNew)
            {
                // Removed locale
                changes.Add(FieldChange.Remove($"{basePath}.{locale}",
                    JsonSerializer.Serialize(oldStrings, JsonOptions)));
            }
            else if (hasOld && hasNew)
            {
                // Compare fields within locale
                CompareField(changes, $"{basePath}.{locale}.Name", oldStrings!.Name, newStrings!.Name);
                CompareField(changes, $"{basePath}.{locale}.Description", oldStrings.Description, newStrings.Description);
                CompareField(changes, $"{basePath}.{locale}.KitchenName", oldStrings.KitchenName, newStrings.KitchenName);
            }
        }
    }

    private static void CompareOptions(
        List<FieldChange> changes,
        List<ModifierOptionState> oldOptions,
        List<ModifierOptionState> newOptions)
    {
        var oldById = oldOptions.ToDictionary(o => o.OptionId);
        var newById = newOptions.ToDictionary(o => o.OptionId);

        // Added options
        foreach (var id in newById.Keys.Except(oldById.Keys))
        {
            var opt = newById[id];
            changes.Add(FieldChange.Add("Options[]",
                JsonSerializer.Serialize(new { opt.OptionId, Name = GetDefaultName(opt.Content), opt.PriceAdjustment, opt.IsDefault }, JsonOptions)));
        }

        // Removed options
        foreach (var id in oldById.Keys.Except(newById.Keys))
        {
            var opt = oldById[id];
            changes.Add(FieldChange.Remove("Options[]",
                JsonSerializer.Serialize(new { opt.OptionId, Name = GetDefaultName(opt.Content), opt.PriceAdjustment, opt.IsDefault }, JsonOptions)));
        }

        // Modified options
        foreach (var id in oldById.Keys.Intersect(newById.Keys))
        {
            var oldOpt = oldById[id];
            var newOpt = newById[id];

            CompareField(changes, $"Options[{id}].Name", GetDefaultName(oldOpt.Content), GetDefaultName(newOpt.Content));
            CompareField(changes, $"Options[{id}].PriceAdjustment", oldOpt.PriceAdjustment, newOpt.PriceAdjustment);
            CompareField(changes, $"Options[{id}].IsDefault", oldOpt.IsDefault, newOpt.IsDefault);
            CompareField(changes, $"Options[{id}].DisplayOrder", oldOpt.DisplayOrder, newOpt.DisplayOrder);
            CompareField(changes, $"Options[{id}].IsActive", oldOpt.IsActive, newOpt.IsActive);
        }
    }
}
