namespace OpenApparatus.Topology.Generators;

/// <summary>
/// Controls how rectangle (1×2) rooms are oriented when placed by
/// <see cref="GridDominoGenerator"/>.
/// </summary>
public enum RectangleOrientation
{
    /// <summary>Each rectangle's orientation is picked at random per placement.</summary>
    Random,

    /// <summary>
    /// All rectangles span along the floor's length axis (+Z) — i.e. each rectangle
    /// occupies a 1-tile-wide × 2-tile-long footprint.
    /// </summary>
    LengthWise,

    /// <summary>
    /// All rectangles span along the floor's width axis (+X) — i.e. each rectangle
    /// occupies a 2-tile-wide × 1-tile-long footprint.
    /// </summary>
    WidthWise,
}
