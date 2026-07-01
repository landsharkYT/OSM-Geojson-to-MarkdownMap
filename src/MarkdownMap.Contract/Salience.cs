using System;
using System.Collections.Generic;

namespace MarkdownMap.Contract;

/// <summary>
/// Narrative-salience classification (ADR-0018): how scene-worthy a Feature is, keyed on the
/// normalized <c>class.subclass</c> category (a contract field, so both stages agree). Splits the
/// old broad <c>civic</c> into public institutions (core) vs private practices (budgeted), and keeps
/// crowd-able categories (artwork, food, shops) out of the always-promote core.
/// </summary>
public static class SalienceClassifier
{
    public const string Core = "core";           // always promotes
    public const string Budgeted = "budgeted";   // competes for the per-District budget
    public const string Clustered = "clustered"; // never its own token

    // Public civic institutions → core; private practices → budgeted.
    private static readonly HashSet<string> PrivateCivic = new(StringComparer.Ordinal)
    { "dentist", "clinic", "pharmacy", "doctors", "veterinary" };

    // Commemorative / decorative landmarks (public art, viewpoints, memorials, monuments) proliferate
    // and are "walk-past" set-dressing → budgeted; the rest (worship, historic, museum, gallery,
    // attraction) are destinations you enter → core (ADR-0018).
    private static readonly HashSet<string> BudgetedLandmark = new(StringComparer.Ordinal)
    { "artwork", "viewpoint", "memorial", "monument" };

    // Destination-scale leisure venues → core; facilities (pool, rink, gym, playground…) → budgeted
    // (ADR-0018): a pool basin is not a stadium, and unnamed ones would otherwise flood the map.
    private static readonly HashSet<string> CoreLeisure = new(StringComparer.Ordinal)
    { "marina", "stadium", "sports_centre", "golf_course" };

    // Worship subclasses — the one category singular/evocative enough to promote while unnamed.
    private static readonly HashSet<string> Worship = new(StringComparer.Ordinal)
    { "place_of_worship", "church", "cathedral", "chapel", "mosque", "synagogue", "temple" };

    /// <summary>Salience class for a normalized <c>class.subclass</c> category.</summary>
    public static string Of(string? category)
    {
        if (string.IsNullOrEmpty(category)) return Budgeted;
        var (klass, sub) = Split(category!);
        return klass switch
        {
            "landmark" => BudgetedLandmark.Contains(sub) ? Budgeted : Core,
            "civic" => PrivateCivic.Contains(sub) ? Budgeted : Core,
            "leisure" => CoreLeisure.Contains(sub) ? Core : Budgeted,
            "residential" => Clustered,
            _ => Budgeted, // food, shop, lodging, transit …
        };
    }

    /// <summary>
    /// True for a worship category. An unnamed Feature promotes only if worship (the canonical
    /// "the church" case); every other unnamed Feature clusters (ADR-0012, refined).
    /// </summary>
    public static bool IsWorship(string? category)
    {
        if (string.IsNullOrEmpty(category)) return false;
        var (klass, sub) = Split(category!);
        return klass == "landmark" && Worship.Contains(sub);
    }

    private static (string klass, string sub) Split(string category)
    {
        int dot = category.IndexOf('.');
        return dot < 0 ? (category, "") : (category.Substring(0, dot), category.Substring(dot + 1));
    }
}
