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
/// per-room interior MeshData in the assembler.
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
        // Corners: A* on +N side (RoomA), B* on -N side. Suffix 0=floor at start,
        // 1=ceiling at start, 2=ceiling at end, 3=floor at end.
        Vector3 A0 = slab.Corner( true, 0f, 0f), A1 = slab.Corner( true, 0f, h);
        Vector3 A2 = slab.Corner( true, slab.Length, h), A3 = slab.Corner( true, slab.Length, 0f);
        Vector3 B0 = slab.Corner(false, 0f, 0f), B1 = slab.Corner(false, 0f, h);
        Vector3 B2 = slab.Corner(false, slab.Length, h), B3 = slab.Corner(false, slab.Length, 0f);

        // RoomA face (normal +N, viewed from RoomA's interior)
        b.AddQuadAutoUv(SubmeshIndex.Walls, A0, A3, A2, A1);
        // RoomB face (normal -N)
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
        const float EPS = 1e-5f;

        // Sort openings by offset and validate fit + non-overlap.
        var openings = new List<Opening>(door.Openings);
        openings.Sort((a, c) => a.OffsetAlongEdge.CompareTo(c.OffsetAlongEdge));
        for (int i = 0; i < openings.Count; i++)
        {
            var op = openings[i];
            if (op.OffsetAlongEdge < -EPS || op.OffsetAlongEdge + op.Width > slab.Length + EPS)
                throw new InvalidOperationException(
                    $"Opening (offset {op.OffsetAlongEdge}, width {op.Width}) does not fit in wall length {slab.Length:F3}.");
            if (op.Height > h + EPS)
                throw new InvalidOperationException(
                    $"Opening height {op.Height} exceeds wall height {h}.");
            if (i > 0 && op.OffsetAlongEdge < openings[i - 1].OffsetAlongEdge + openings[i - 1].Width - EPS)
                throw new InvalidOperationException(
                    $"Openings overlap: opening {i - 1} ends at " +
                    $"{openings[i - 1].OffsetAlongEdge + openings[i - 1].Width:F3} but opening {i} starts at {op.OffsetAlongEdge:F3}.");
        }

        var b = new MeshDataBuilder();

        // Solid wall sections between (and around) openings — full height, both faces.
        float prev = 0f;
        for (int i = 0; i < openings.Count; i++)
        {
            var op = openings[i];
            if (op.OffsetAlongEdge > prev + EPS)
                EmitFullHeightSection(b, slab, prev, op.OffsetAlongEdge, h);
            prev = op.OffsetAlongEdge + op.Width;
        }
        if (slab.Length > prev + EPS)
            EmitFullHeightSection(b, slab, prev, slab.Length, h);

        // Lintel above each opening — only if the opening is shorter than the wall.
        foreach (var op in openings)
        {
            if (op.Height < h - EPS)
                EmitLintel(b, slab, op.OffsetAlongEdge, op.OffsetAlongEdge + op.Width, op.Height, h);
        }

        // Sill panel below each window (SillHeight > 0) — wall body between the floor
        // and the bottom of the opening. Uses EmitLintel since geometry is identical.
        foreach (var op in openings)
        {
            if (op.SillHeight > EPS)
                EmitLintel(b, slab, op.OffsetAlongEdge, op.OffsetAlongEdge + op.Width, 0f, op.SillHeight);
        }

        // Top face (full length, unaffected by openings).
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner(false, 0f,          h),
            slab.Corner(false, slab.Length, h),
            slab.Corner( true, slab.Length, h),
            slab.Corner( true, 0f,          h));

        // Bottom strips. Doors break the bottom strip (their threshold floor goes in
        // the Floor submesh below); windows do not, since the wall is solid below them.
        prev = 0f;
        for (int i = 0; i < openings.Count; i++)
        {
            var op = openings[i];
            if (op.IsWindow) continue;
            if (op.OffsetAlongEdge > prev + EPS)
                EmitBottomStrip(b, slab, prev, op.OffsetAlongEdge);
            prev = op.OffsetAlongEdge + op.Width;
        }
        if (slab.Length > prev + EPS)
            EmitBottomStrip(b, slab, prev, slab.Length);

        // Side caps — full slab cross-section at the wall ends.
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

        // Tunnel inner faces (visible inside each opening): left side, right side,
        // ceiling at the lintel's underside (or wall top for full-height openings),
        // and a bottom — either a threshold floor (door, in Floor submesh) or a sill
        // top (window, in Walls submesh).
        foreach (var op in openings)
        {
            float doorEnd = op.OffsetAlongEdge + op.Width;
            float yBot = op.SillHeight;
            // Left side (at op.OffsetAlongEdge, normal +D)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, op.OffsetAlongEdge, yBot),
                slab.Corner(false, op.OffsetAlongEdge, yBot),
                slab.Corner(false, op.OffsetAlongEdge, op.Height),
                slab.Corner( true, op.OffsetAlongEdge, op.Height));
            // Right side (at doorEnd, normal -D)
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner(false, doorEnd, yBot),
                slab.Corner( true, doorEnd, yBot),
                slab.Corner( true, doorEnd, op.Height),
                slab.Corner(false, doorEnd, op.Height));
            // Ceiling — lintel underside (or wall-top span if full-height opening).
            // Wound so the normal points -Y, i.e. visible when looking up through the
            // opening from below. The intuitive +N→+N→-N→-N order would put the
            // normal facing up, leaving the underside back-face culled.
            float ceilY = op.Height < h - EPS ? op.Height : h;
            b.AddQuadAutoUv(SubmeshIndex.Walls,
                slab.Corner( true, op.OffsetAlongEdge, ceilY),
                slab.Corner(false, op.OffsetAlongEdge, ceilY),
                slab.Corner(false, doorEnd,            ceilY),
                slab.Corner( true, doorEnd,            ceilY));
            // Bottom: door = threshold floor (Floor submesh, normal +Y),
            //         window = sill top (Walls submesh, normal +Y).
            int bottomSubmesh = op.IsWindow ? SubmeshIndex.Walls : SubmeshIndex.Floor;
            b.AddQuadAutoUv(bottomSubmesh,
                slab.Corner(false, op.OffsetAlongEdge, yBot),
                slab.Corner( true, op.OffsetAlongEdge, yBot),
                slab.Corner( true, doorEnd,            yBot),
                slab.Corner(false, doorEnd,            yBot));
        }

        b.EnsureSubmeshCount(SubmeshIndex.Count);
        return b.ToMeshData();
    }

    /// <summary>Emits the CellA + CellB face of a full-height wall section [xStart, xEnd] × [0, h].</summary>
    static void EmitFullHeightSection(MeshDataBuilder b, SlabFrame slab, float xStart, float xEnd, float h)
    {
        // CellA face (normal +N), CCW from CellA-side view.
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner( true, xStart, 0f),
            slab.Corner( true, xEnd,   0f),
            slab.Corner( true, xEnd,   h),
            slab.Corner( true, xStart, h));
        // CellB face (normal -N), reversed winding.
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner(false, xStart, 0f),
            slab.Corner(false, xStart, h),
            slab.Corner(false, xEnd,   h),
            slab.Corner(false, xEnd,   0f));
    }

    /// <summary>Emits CellA + CellB faces for a lintel — wall section [xStart, xEnd] × [yBot, yTop].</summary>
    static void EmitLintel(MeshDataBuilder b, SlabFrame slab, float xStart, float xEnd, float yBot, float yTop)
    {
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner( true, xStart, yBot),
            slab.Corner( true, xEnd,   yBot),
            slab.Corner( true, xEnd,   yTop),
            slab.Corner( true, xStart, yTop));
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner(false, xStart, yBot),
            slab.Corner(false, xStart, yTop),
            slab.Corner(false, xEnd,   yTop),
            slab.Corner(false, xEnd,   yBot));
    }

    /// <summary>Emits the bottom face strip [xStart, xEnd] at y=0 (normal -Y).</summary>
    static void EmitBottomStrip(MeshDataBuilder b, SlabFrame slab, float xStart, float xEnd)
    {
        b.AddQuadAutoUv(SubmeshIndex.Walls,
            slab.Corner( true, xStart, 0f),
            slab.Corner( true, xEnd,   0f),
            slab.Corner(false, xEnd,   0f),
            slab.Corner(false, xStart, 0f));
    }

    // -------------------- Slab frame helper --------------------

    /// <summary>
    /// A coordinate frame for a wall slab: an origin (segment start), a "direction"
    /// axis along the segment, a "normal" axis perpendicular to it (in XZ, +N = RoomA side),
    /// and a length. <see cref="Corner"/> turns slab-local (along, height, side) into world XYZ.
    /// </summary>
    readonly struct SlabFrame
    {
        public readonly Vector3 Origin;       // world position of segment start at y=0
        public readonly Vector3 DirectionXZ;  // unit vector along segment in XZ
        public readonly Vector3 NormalXZ;     // unit vector 90° CCW from DirectionXZ in XZ; +N points to RoomA
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
            var n2 = seg.Normal; // 90° CCW from d in XZ; +N points "left" of walker = RoomA side
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
