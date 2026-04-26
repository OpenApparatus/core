using System;
using System.Numerics;
using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// Builds wall geometry for one <see cref="Adjacency"/>. The wall is a thick
/// slab centered on the adjacency's <see cref="Adjacency.SharedSegment"/>, split
/// equally on each side of the segment, running from y=0 to the requested height.
///
/// Output:
///   • <see cref="Passage.Closed"/>  → 6-face closed wall slab in submesh
///                                     <see cref="SubmeshIndex.Walls"/>.
///   • <see cref="Passage.Open"/>    → empty MeshData (no geometry).
///   • <see cref="Passage.Doorway"/> → wall slab with rectangular tunnel
///                                     through the full thickness.
///
/// All output is written to submesh <see cref="SubmeshIndex.Walls"/>; the
/// returned mesh has <see cref="SubmeshIndex.Count"/> submeshes (Floor and
/// Ceiling submeshes are present but empty), so it can be combined with
/// per-cell interior MeshData in the assembler.
/// </summary>
public sealed class BoundaryWallBuilder
{
    public MeshData Build(Adjacency adjacency, float wallThickness, float wallHeight)
    {
        if (adjacency is null) throw new ArgumentNullException(nameof(adjacency));
        if (wallThickness <= 0f) throw new ArgumentOutOfRangeException(nameof(wallThickness));
        if (wallHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(wallHeight));

        return adjacency.Passage switch
        {
            Passage.Open _ => EmptyResult(),
            Passage.Closed _ => BuildClosed(adjacency, wallThickness, wallHeight),
            Passage.Doorway d => BuildDoorway(adjacency, wallThickness, wallHeight, d),
            _ => throw new InvalidOperationException(
                $"Unknown passage type: {adjacency.Passage.GetType().Name}"),
        };
    }

    static MeshData EmptyResult()
    {
        var b = new MeshDataBuilder();
        b.EnsureSubmeshCount(SubmeshIndex.Count);
        return b.ToMeshData();
    }

    // -------------------- Closed wall --------------------

    static MeshData BuildClosed(Adjacency adj, float t, float h)
    {
        var slab = SlabFrame.From(adj.SharedSegment, t);
        var b = new MeshDataBuilder();
        EmitClosedWallFaces(b, slab, h);
        b.EnsureSubmeshCount(SubmeshIndex.Count);
        return b.ToMeshData();
    }

    /// <summary>Emits the 6 faces of a fully-closed wall slab.</summary>
    static void EmitClosedWallFaces(MeshDataBuilder b, SlabFrame slab, float h)
    {
        // Corners: A* on +N side (CellA), B* on -N side. Suffix 0=floor at start,
        // 1=ceiling at start, 2=ceiling at end, 3=floor at end.
        Vector3 A0 = slab.Corner( true, 0f, 0f), A1 = slab.Corner( true, 0f, h);
        Vector3 A2 = slab.Corner( true, slab.Length, h), A3 = slab.Corner( true, slab.Length, 0f);
        Vector3 B0 = slab.Corner(false, 0f, 0f), B1 = slab.Corner(false, 0f, h);
        Vector3 B2 = slab.Corner(false, slab.Length, h), B3 = slab.Corner(false, slab.Length, 0f);

        // CellA face (normal +N, viewed from CellA's interior)
        b.AddQuadAutoUv(SubmeshIndex.Walls, A0, A3, A2, A1);
        // CellB face (normal -N)
        b.AddQuadAutoUv(SubmeshIndex.Walls, B0, B1, B2, B3);
        // Top (normal +Y)
        b.AddQuadAutoUv(SubmeshIndex.Walls, B1, B2, A2, A1);
        // Bottom (normal -Y)
        b.AddQuadAutoUv(SubmeshIndex.Walls, A0, A3, B3, B0);
        // Start cap (normal -D)
        b.AddQuadAutoUv(SubmeshIndex.Walls, A0, A1, B1, B0);
        // End cap (normal +D)
        b.AddQuadAutoUv(SubmeshIndex.Walls, A3, B3, B2, A2);
    }

    // -------------------- Doorway wall --------------------

    static MeshData BuildDoorway(Adjacency adj, float t, float h, Passage.Doorway door)
    {
        var slab = SlabFrame.From(adj.SharedSegment, t);

        float doorOffset = door.OffsetAlongEdge;
        float doorWidth = door.Width;
        float doorHeight = door.Height;
        float doorEnd = doorOffset + doorWidth;

        if (doorOffset < 0f || doorEnd > slab.Length)
            throw new InvalidOperationException(
                $"Doorway (offset {doorOffset}, width {doorWidth}) does not fit in wall length {slab.Length:F3}.");
        if (doorHeight > h)
            throw new InvalidOperationException(
                $"Doorway height {doorHeight} exceeds wall height {h}.");

        var b = new MeshDataBuilder();

        // Convenience flags for edge cases.
        bool hasLeftJamb  = doorOffset > 1e-5f;
        bool hasRightJamb = doorEnd    < slab.Length - 1e-5f;
        bool hasLintel    = doorHeight < h - 1e-5f;

        // -------- CellA face (normal +N) split into left jamb / lintel / right jamb --------
        if (hasLeftJamb)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, 0f,         0f),
                slab.Corner( true, doorOffset, 0f),
                slab.Corner( true, doorOffset, h),
                slab.Corner( true, 0f,         h));
        if (hasRightJamb)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, doorEnd,     0f),
                slab.Corner( true, slab.Length, 0f),
                slab.Corner( true, slab.Length, h),
                slab.Corner( true, doorEnd,     h));
        if (hasLintel)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, doorOffset, doorHeight),
                slab.Corner( true, doorEnd,    doorHeight),
                slab.Corner( true, doorEnd,    h),
                slab.Corner( true, doorOffset, h));

        // -------- CellB face (normal -N) — same splits, mirrored winding --------
        if (hasLeftJamb)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner(false, 0f,         0f),
                slab.Corner(false, 0f,         h),
                slab.Corner(false, doorOffset, h),
                slab.Corner(false, doorOffset, 0f));
        if (hasRightJamb)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner(false, doorEnd,     0f),
                slab.Corner(false, doorEnd,     h),
                slab.Corner(false, slab.Length, h),
                slab.Corner(false, slab.Length, 0f));
        if (hasLintel)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner(false, doorOffset, doorHeight),
                slab.Corner(false, doorOffset, h),
                slab.Corner(false, doorEnd,    h),
                slab.Corner(false, doorEnd,    doorHeight));

        // -------- Top face (full length, unaffected by doorway) --------
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner(false, 0f,         h),
            slab.Corner(false, slab.Length, h),
            slab.Corner( true, slab.Length, h),
            slab.Corner( true, 0f,         h));

        // -------- Bottom face — split by the door opening --------
        if (hasLeftJamb)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, 0f,         0f),
                slab.Corner( true, doorOffset, 0f),
                slab.Corner(false, doorOffset, 0f),
                slab.Corner(false, 0f,         0f));
        if (hasRightJamb)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, doorEnd,     0f),
                slab.Corner( true, slab.Length, 0f),
                slab.Corner(false, slab.Length, 0f),
                slab.Corner(false, doorEnd,     0f));

        // -------- Side caps (start, end) — full slab cross-section, unaffected by door --------
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner( true, 0f, 0f),
            slab.Corner( true, 0f, h),
            slab.Corner(false, 0f, h),
            slab.Corner(false, 0f, 0f));
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner( true, slab.Length, 0f),
            slab.Corner(false, slab.Length, 0f),
            slab.Corner(false, slab.Length, h),
            slab.Corner( true, slab.Length, h));

        // -------- Tunnel inner faces (visible inside the doorway) --------
        // Tunnel left face (at door start, normal +D pointing toward door interior)
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner( true, doorOffset, 0f),
            slab.Corner(false, doorOffset, 0f),
            slab.Corner(false, doorOffset, doorHeight),
            slab.Corner( true, doorOffset, doorHeight));
        // Tunnel right face (at door end, normal -D)
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner(false, doorEnd, 0f),
            slab.Corner( true, doorEnd, 0f),
            slab.Corner( true, doorEnd, doorHeight),
            slab.Corner(false, doorEnd, doorHeight));
        // Tunnel ceiling (lintel underside, normal -Y), present only if there's a lintel
        if (hasLintel)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, doorOffset, doorHeight),
                slab.Corner( true, doorEnd,    doorHeight),
                slab.Corner(false, doorEnd,    doorHeight),
                slab.Corner(false, doorOffset, doorHeight));

        b.EnsureSubmeshCount(SubmeshIndex.Count);
        return b.ToMeshData();
    }

    // -------------------- Slab frame helper --------------------

    /// <summary>
    /// A coordinate frame for a wall slab: an origin (segment start), a "direction"
    /// axis along the segment, a "normal" axis perpendicular to it (in XZ, +N = CellA side),
    /// and a length. <see cref="Corner"/> turns slab-local (along, height, side) into world XYZ.
    /// </summary>
    readonly struct SlabFrame
    {
        public readonly Vector3 Origin;       // world position of segment start at y=0
        public readonly Vector3 DirectionXZ;  // unit vector along segment in XZ
        public readonly Vector3 NormalXZ;     // unit vector 90° CCW from DirectionXZ in XZ; +N points to CellA
        public readonly float HalfThickness;
        public readonly float Length;

        SlabFrame(Vector3 origin, Vector3 dir, Vector3 normal, float halfT, float length)
        {
            Origin = origin;
            DirectionXZ = dir;
            NormalXZ = normal;
            HalfThickness = halfT;
            Length = length;
        }

        public static SlabFrame From(EdgeSegment seg, float thickness)
        {
            var origin = new Vector3(seg.Start.X, 0f, seg.Start.Y);
            var d2 = seg.Direction;
            var n2 = seg.Normal; // 90° CCW from d in XZ; +N points "left" of walker = CellA side
            var dir = new Vector3(d2.X, 0f, d2.Y);
            var nrm = new Vector3(n2.X, 0f, n2.Y);
            return new SlabFrame(origin, dir, nrm, thickness * 0.5f, seg.Length);
        }

        /// <summary>
        /// World position of a slab corner. <paramref name="cellASide"/> picks the +N/-N face;
        /// <paramref name="along"/> is the distance from the segment start along the wall;
        /// <paramref name="height"/> is the world Y.
        /// </summary>
        public Vector3 Corner(bool cellASide, float along, float height)
        {
            float side = cellASide ? +HalfThickness : -HalfThickness;
            return Origin + DirectionXZ * along + NormalXZ * side + new Vector3(0f, height, 0f);
        }
    }
}
