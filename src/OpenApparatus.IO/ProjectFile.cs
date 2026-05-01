using System.Collections.Generic;

namespace OpenApparatus.IO;

/// <summary>Serializable mirror of every authored field on the editor. Each
/// property is written in camelCase; null / default values are omitted on write
/// to keep saves small.
///
/// Editors are responsible for converting their own state into a
/// <see cref="ProjectFile"/> before calling <see cref="ProjectIO.Save"/>, and
/// applying a loaded file back to their state after calling
/// <see cref="ProjectIO.Load"/>. The desktop Avalonia editor implements this
/// via <c>MainWindowViewModel.ToProjectFile()</c> / <c>RestoreFromProjectFile()</c>.
/// </summary>
public sealed class ProjectFile
{
    public string? Version { get; set; } = ProjectIO.CurrentVersion;
    public string? Title { get; set; }

    // Grid + measurements
    public int GridWidth { get; set; }
    public int GridLength { get; set; }
    public float TileSize { get; set; }
    public float WallThickness { get; set; }
    public float WallHeight { get; set; }
    public float DoorWidth { get; set; }
    public float DoorHeight { get; set; }
    public float WindowWidth { get; set; }
    public float WindowHeight { get; set; }
    public float WindowSillHeight { get; set; }
    public int GridSubdivision { get; set; }
    public float DefaultObjectY { get; set; }

    // Defaults
    public float[]? DefaultFloorColor { get; set; }
    public float[]? DefaultCeilingColor { get; set; }

    // Tile → room ownership grid (flattened row-major).
    public int[]? RoomGrid { get; set; }

    // Per-room palettes / state.
    public Dictionary<int, float[]>? RoomFloorColors { get; set; }
    public Dictionary<int, float[]>? RoomCeilingColors { get; set; }
    public Dictionary<int, float[]>? RoomSingleWallColors { get; set; }
    public Dictionary<int, string>? RoomNames { get; set; }
    public List<int>? MultiColorRoomIds { get; set; }

    // Per-wall colour overrides keyed by "{roomId}_{midXmm}_{midZmm}".
    public Dictionary<string, float[]>? WallColors { get; set; }

    // Passage overrides — adjacency identity is reconstructed from
    // start/end mm-coordinates, since the in-memory Adjacency object
    // doesn't survive serialization.
    public List<PassageOverrideEntry>? PassageOverrides { get; set; }

    // Object types + instances.
    public List<ObjectTypeEntry>? ObjectTypes { get; set; }
    public List<ObjectInstanceEntry>? Objects { get; set; }

    // Camera state.
    public string? CameraView { get; set; }
    public double ZoomFactor { get; set; }
    public double PanOffsetX { get; set; }
    public double PanOffsetY { get; set; }
    public float IsoYaw { get; set; }
    public float IsoPitch { get; set; }
    public float IsoDistance { get; set; }
    public float IsoPivotX { get; set; }
    public float IsoPivotY { get; set; }
    public float IsoPivotZ { get; set; }

    // Placement constraints — straight POCO copy.
    public PlacementConstraints? Constraints { get; set; }
}

public sealed class PassageOverrideEntry
{
    public float StartX { get; set; }
    public float StartZ { get; set; }
    public float EndX { get; set; }
    public float EndZ { get; set; }
    public string Kind { get; set; } = "Closed"; // Closed / Open / Doorway
    public List<OpeningEntry>? Openings { get; set; }
}

public sealed class OpeningEntry
{
    public float Offset { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float SillHeight { get; set; }
    public bool HingeAtEnd { get; set; }
    public bool SwingNegative { get; set; }
}

public sealed class ObjectTypeEntry
{
    public string Name { get; set; } = "";
    public string Shape { get; set; } = "Cube";
    public float[]? Color { get; set; }
    public float Size { get; set; } = 0.3f;
}

public sealed class ObjectInstanceEntry
{
    public int Slot { get; set; }
    public int OwningRoomId { get; set; } = -1;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Rotation { get; set; }
}
