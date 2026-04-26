using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenApparatus.Topology;

/// <summary>
/// A width × depth axis-aligned rectangle, with its local origin at the
/// south-west (min) corner and edges in the order [South, East, North, West].
///
/// Coordinate convention: +X = east, +Z = north (in 2D, +Y component of the
/// stored Vector2 represents the +Z world axis).
/// </summary>
public sealed class RectangleShape : ICellShape
{
    public float Width { get; }
    public float Depth { get; }

    public RectangleShape(float width, float depth)
    {
        if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
        if (depth <= 0f) throw new ArgumentOutOfRangeException(nameof(depth));
        Width = width;
        Depth = depth;
    }

    /// <summary>
    /// Outline ordered CCW from above:
    /// [0] South: (0,0) → (W,0)
    /// [1] East:  (W,0) → (W,D)
    /// [2] North: (W,D) → (0,D)
    /// [3] West:  (0,D) → (0,0)
    /// </summary>
    public IReadOnlyList<EdgeSegment> GetOutline() =>
    [
        new EdgeSegment(new Vector2(0f,    0f),    new Vector2(Width, 0f)),
        new EdgeSegment(new Vector2(Width, 0f),    new Vector2(Width, Depth)),
        new EdgeSegment(new Vector2(Width, Depth), new Vector2(0f,    Depth)),
        new EdgeSegment(new Vector2(0f,    Depth), new Vector2(0f,    0f)),
    ];

    public Bounds2D GetLocalBounds() =>
        new(Vector2.Zero, new Vector2(Width, Depth));

    public override string ToString() => $"Rectangle({Width:F2} x {Depth:F2})";
}
