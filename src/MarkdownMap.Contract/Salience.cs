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

    // Crowd-able landmark subclasses (public art, viewpoints) → budgeted; the rest (worship,
    // historic, museum, gallery, attraction, monument) → core.
    private static readonly HashSet<string> BudgetedLandmark = new(StringComparer.Ordinal)
    { "artwork", "viewpoint" };

    // Sizeable leisure venues → core; small facilities (gym, playground, pitch…) → budgeted.
    private static readonly HashSet<string> CoreLeisure = new(StringComparer.Ordinal)
    { "marina", "stadium", "sports_centre", "golf_course", "swimming_pool", "ice_rink" };

    /// <summary>Salience class for a normalized <c>class.subclass</c> category.</summary>
    public static string Of(string? category)
    {
        if (string.IsNullOrEmpty(category)) return Budgeted;
        int dot = category!.IndexOf('.');
        string klass = dot < 0 ? category : category.Substring(0, dot);
        string sub = dot < 0 ? "" : category.Substring(dot + 1);
        return klass switch
        {
            "landmark" => BudgetedLandmark.Contains(sub) ? Budgeted : Core,
            "civic" => PrivateCivic.Contains(sub) ? Budgeted : Core,
            "leisure" => CoreLeisure.Contains(sub) ? Core : Budgeted,
            "residential" => Clustered,
            _ => Budgeted, // food, shop, lodging, transit …
        };
    }
}
