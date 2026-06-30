namespace MarkdownMap.Contract;

/// <summary>
/// Coarse pipeline phases reported during a build. UI-agnostic on purpose (ADR-0010):
/// the pipeline says *what happened*; consumers (the Explorer) decide how to display it.
/// The CLI passes no callback and pays nothing.
/// </summary>
public enum BuildPhase
{
    /// <summary>Streaming the OSM XML. <c>value</c> = bytes read so far (vs. stream length).</summary>
    Parsing = 1,

    /// <summary>Running the Generator (proximity graph, districts, render). <c>value</c> = feature count.</summary>
    Building = 2,

    /// <summary>Serializing the MapModel to JSON. <c>value</c> = 0.</summary>
    Serializing = 3,
}

/// <summary>
/// Optional progress sink threaded through the pipeline. <c>value</c>'s meaning depends on
/// <paramref name="phase"/> (see <see cref="BuildPhase"/>). Calls must be cheap — they run
/// on the (blocked) build thread between units of work.
/// </summary>
public delegate void BuildProgress(BuildPhase phase, long value);
