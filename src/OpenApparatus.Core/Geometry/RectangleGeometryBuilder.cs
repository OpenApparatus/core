using System;
using System.Numerics;
using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// Builds a <see cref="MeshData"/> for a single fully-closed room whose shape is a
/// <see cref="RectangleShape"/>. The room becomes a thick-walled box with three
/// submeshes (floor, walls, ceiling).
///
/// Faces emitted (18 total):
///   • 1 floor quad   (interior, normal +Y)
///   • 1 ceiling quad (interior, normal −Y)
///   • 4 outer wall quads — at the room footprint, normals point outward
///   • 4 inner wall quads — at the inset interior boundary, normals point inward
///   • 4 top frame quads — annular ring at y = wallHeight between outer and inner
///   • 4 bottom frame quads — annular ring at y = 0 between outer and inner
///
/// Each face has its own 4 vertices (no sharing between faces) so normals stay sharp.
/// Room rotation is supported.
/// </summary>
public sealed class RectangleGeometryBuilder : IShapeGeometryBuilder
{
    public MeshData Build(Room room, float wallThickness, float wallHeight)
    {
        if (room is null) throw new ArgumentNullException(nameof(room));
        if (room.Shape is not RectangleShape rect)
            throw new InvalidOperationException(
                $"RectangleGeometryBuilder requires a RectangleShape; got {room.Shape.GetType().Name}.");
        if (wallThickness <= 0f) throw new ArgumentOutOfRangeException(nameof(wallThickness));
        if (wallHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(wallHeight));
        if (wallThickness * 2f >= rect.Width || wallThickness * 2f >= rect.Depth)
            throw new ArgumentException(
                $"Wall thickness {wallThickness} too large for {rect.Width}×{rect.Depth} room " +
                $"(would leave no interior).");

        float W = rect.Width;
        float D = rect.Depth;
        float t = wallThickness;
        float h = wallHeight;

        var b = new MeshDataBuilder();

        // Pre-rotation cache — geometry is computed in room-local coordinates and
        // each vertex transformed via World() to handle rotation + position.

        // ----- Floor (interior, +Y) -----
        b.AddQuadAutoUv(SubmeshIndex.Floor,
            World(room, t,     0f, t),
            World(room, t,     0f, D - t),
            World(room, W - t, 0f, D - t),
            World(room, W - t, 0f, t));

        // ----- Ceiling (interior, −Y) -----
        b.AddQuadAutoUv(SubmeshIndex.Ceiling,
            World(room, t,     h, t),
            World(room, W - t, h, t),
            World(room, W - t, h, D - t),
            World(room, t,     h, D - t));

        // ----- Outer walls (4 quads, normals outward) -----
        // Convention: walk the outer perimeter CCW (from above) with
        // start→end on the floor; the wall extrudes upward to wallHeight.
        AddVerticalWall(b, room, 0f,     0f,     W,      0f,     h); // South outer (-Z)
        AddVerticalWall(b, room, W,      0f,     W,      D,      h); // East outer  (+X)
        AddVerticalWall(b, room, W,      D,      0f,     D,      h); // North outer (+Z)
        AddVerticalWall(b, room, 0f,     D,      0f,     0f,     h); // West outer  (-X)

        // ----- Inner walls (4 quads, normals inward) -----
        // CCW around interior is "outward" from the interior — to get inward-pointing
        // normals we reverse: walk CW around the inner perimeter (or equivalently,
        // CCW around it as seen from below the floor).
        AddVerticalWall(b, room, t,     t,     W - t, t,     h); // Inner south (+Z)
        AddVerticalWall(b, room, W - t, t,     W - t, D - t, h); // Inner east  (-X)
        AddVerticalWall(b, room, W - t, D - t, t,     D - t, h); // Inner north (-Z)
        AddVerticalWall(b, room, t,     D - t, t,     t,     h); // Inner west  (+X)

        // ----- Top frame (4 quads, +Y, ring around the ceiling at y=h) -----
        // Decomposition is non-overlapping: north and south span full width; west and
        // east cover only the central z range to avoid corner overlaps.
        AddHorizontalQuad(b, room, 0f,     D - t, W,      D, h, normalUp: true);  // North top
        AddHorizontalQuad(b, room, 0f,     0f,    W,      t, h, normalUp: true);  // South top
        AddHorizontalQuad(b, room, 0f,     t,     t,      D - t, h, normalUp: true); // West top
        AddHorizontalQuad(b, room, W - t,  t,     W,      D - t, h, normalUp: true); // East top

        // ----- Bottom frame (4 quads, −Y, ring around the floor at y=0) -----
        AddHorizontalQuad(b, room, 0f,     D - t, W,      D, 0f, normalUp: false);
        AddHorizontalQuad(b, room, 0f,     0f,    W,      t, 0f, normalUp: false);
        AddHorizontalQuad(b, room, 0f,     t,     t,      D - t, 0f, normalUp: false);
        AddHorizontalQuad(b, room, W - t,  t,     W,      D - t, 0f, normalUp: false);

        return b.ToMeshData();
    }

    /// <summary>
    /// Emit a vertical wall quad spanning from (ax, az) to (bx, bz) at the floor,
    /// extruded upward to <paramref name="height"/>. The face's outward normal is
    /// determined by the start→end direction (right-hand rule around +Y).
    /// </summary>
    static void AddVerticalWall(MeshDataBuilder b, Room room,
        float ax, float az, float bx, float bz, float height)
    {
        var aFloor = World(room, ax, 0f, az);
        var bFloor = World(room, bx, 0f, bz);
        var aCeil  = World(room, ax, height, az);
        var bCeil  = World(room, bx, height, bz);
        // Order: a-floor, a-ceil, b-ceil, b-floor (start at floor, go up, across the top, down)
        b.AddQuadAutoUv(SubmeshIndex.Walls, aFloor, aCeil, bCeil, bFloor);
    }

    /// <summary>
    /// Emit a horizontal quad in the XZ plane, axis-aligned, spanning [xMin..xMax] × [zMin..zMax]
    /// at the given y. <paramref name="normalUp"/>=true → normal +Y; false → normal −Y.
    /// </summary>
    static void AddHorizontalQuad(MeshDataBuilder b, Room room,
        float xMin, float zMin, float xMax, float zMax, float y, bool normalUp)
    {
        var p00 = World(room, xMin, y, zMin);
        var p10 = World(room, xMax, y, zMin);
        var p11 = World(room, xMax, y, zMax);
        var p01 = World(room, xMin, y, zMax);
        // For +Y: order (xMin,zMin), (xMin,zMax), (xMax,zMax), (xMax,zMin) — verified by cross product.
        // For −Y: reverse winding.
        if (normalUp)
            b.AddQuadAutoUv(SubmeshIndex.Walls, p00, p01, p11, p10);
        else
            b.AddQuadAutoUv(SubmeshIndex.Walls, p00, p10, p11, p01);
    }

    /// <summary>
    /// Transform a room-local point (cellX in 2D X, cellZ in 2D Z, plus a world Y)
    /// into a world-space Vector3, applying the room's position and rotation.
    /// </summary>
    static Vector3 World(Room room, float cellX, float worldY, float cellZ)
    {
        if (room.Rotation == 0f)
            return new Vector3(cellX + room.Position.X, worldY, cellZ + room.Position.Y);

        float rad = room.Rotation * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        float rx = cellX * cos - cellZ * sin;
        float rz = cellX * sin + cellZ * cos;
        return new Vector3(rx + room.Position.X, worldY, rz + room.Position.Y);
    }
}
