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

    // Civic salience is an ALLOWLIST (ADR-0018 refined): the public institutions a DM orients on are
    // core; EVERYTHING else civic — private practices (dentist/clinic/doctors), `healthcare=*`
    // passthrough (psychotherapist/physiotherapist/alternative), social_facility, offices
    // (government/research), and campus `_building` halls — defaults to budgeted and competes for the
    // promotion budget. Fail-safe toward competing, not flooding: a civic subclass nobody enumerated
    // (a new `healthcare=*` value) budgets rather than silently promoting at institution rank.
    private static readonly HashSet<string> CoreCivicInstitution = new(StringComparer.Ordinal)
    {
        "school", "college", "university", "library", "hospital", "post_office", "townhall",
        "police", "fire_station", "courthouse", "station", "community_centre", "civic", "kindergarten",
    };

    // Commemorative / decorative landmarks — public art, viewpoints, memorials, monuments, and
    // walk-past man_made structures (tower, lighthouse, bridge…) — are set-dressing → budgeted; the
    // rest (worship, historic, museum, gallery, attraction) are destinations you enter → core (ADR-0018).
    // `pier` is NOT here: a named moorage is the waterfront's address, so it promotes as core.
    private static readonly HashSet<string> BudgetedLandmark = new(StringComparer.Ordinal)
    {
        "artwork", "viewpoint", "memorial", "monument",
        "tower", "lighthouse", "water_tower", "bridge", "obelisk", "windmill", "communications_tower",
    };

    // Destination-scale leisure venues → core; facilities (pool, rink, gym, playground…) → budgeted
    // (ADR-0018): a pool basin is not a stadium, and unnamed ones would otherwise flood the map.
    private static readonly HashSet<string> CoreLeisure = new(StringComparer.Ordinal)
    { "marina", "stadium", "sports_centre", "golf_course" };

    // Worship subclasses — the one category singular/evocative enough to promote while unnamed.
    private static readonly HashSet<string> Worship = new(StringComparer.Ordinal)
    { "place_of_worship", "church", "cathedral", "chapel", "mosque", "synagogue", "temple" };

    // Institutional BUILDINGS that repeat many-per-institution (a campus's lecture halls) → budgeted
    // venue-band competitors, not core (ADR-0019): 100+ halls can't all be the always-promote skeleton.
    // Distinguished from the institution itself (amenity/building=university → civic.university, core)
    // by the `_building` subclass suffix the Classifier assigns to a hall.
    private static readonly HashSet<string> AreaRankedBuilding = new(StringComparer.Ordinal)
    { "university_building", "college_building" };

    /// <summary>Salience class for a normalized <c>class.subclass</c> category.</summary>
    public static string Of(string? category)
    {
        if (string.IsNullOrEmpty(category)) return Budgeted;
        var (klass, sub) = Split(category!);
        return klass switch
        {
            "landmark" => BudgetedLandmark.Contains(sub) ? Budgeted : Core,
            "civic" => CoreCivicInstitution.Contains(sub) ? Core : Budgeted,
            "leisure" => CoreLeisure.Contains(sub) ? Core : Budgeted,
            // A dorm is a residential address you go to (like a moorage) → core; hotels compete.
            "lodging" => sub == "student_accommodation" ? Core : Budgeted,
            "residential" => Clustered,
            _ => Budgeted, // food, shop, transit …
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

    /// <summary>
    /// True for a budgeted institutional building (a campus hall) whose importance the Normalizer
    /// nudges by footprint area, so the big halls out-sort the annexes within the promotion budget
    /// (ADR-0019). Keyed on the `_building` subclass the Classifier assigns to distinguish a hall from
    /// the institution itself.
    /// </summary>
    public static bool IsAreaRankedBuilding(string? category)
    {
        if (string.IsNullOrEmpty(category)) return false;
        var (klass, sub) = Split(category!);
        return klass == "civic" && AreaRankedBuilding.Contains(sub);
    }

    private static (string klass, string sub) Split(string category)
    {
        int dot = category.IndexOf('.');
        return dot < 0 ? (category, "") : (category.Substring(0, dot), category.Substring(dot + 1));
    }
}
