using System;
using System.Collections.Generic;
using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// Walks a <see cref="MultiRoomEnvironment"/> and produces one merged <see cref="MeshData"/>
/// per room. The result is a list of <see cref="AssembledRoomMesh"/> in the same
/// order as <see cref="MultiRoomEnvironment.Rooms"/>.
///
/// Per room, the assembler:
///   1. Builds the room's interior (floor + ceiling) via the appropriate
///      <c>I*InteriorBuilder</c> for the room's shape.
///   2. For each adjacency the room touches, decides whether this room owns the
///      wall (lower-id ownership for internal adjacencies; RoomA owns its outer
///      adjacencies). Walls owned by this room are built via
///      <see cref="BoundaryWallBuilder"/> and appended to its mesh parts.
///   3. Combines all parts with <see cref="MeshData.Combine"/>.
///
/// In v1 only <see cref="RectangleShape"/> is supported. Other shapes will
/// throw — extending support means adding a builder for that shape and a
/// dispatch case below.
///
/// Known limitation: at outer corners of a building, walls don't extend past
/// their segment ends, so a t/2 × t/2 square gap remains. To be fixed in a
/// subsequent milestone (corner posts, or wall length extension).
/// </summary>
public sealed class MultiRoomEnvironmentMeshAssembler
{
    readonly RectangleInteriorBuilder _rectInterior = new();
    readonly BoundaryWallBuilder _wallBuilder = new();

    public IReadOnlyList<AssembledRoomMesh> Assemble(
        MultiRoomEnvironment plan, float wallThickness, float wallHeight)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (wallThickness <= 0f) throw new ArgumentOutOfRangeException(nameof(wallThickness));
        if (wallHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(wallHeight));

        // Phase 1: room interiors.
        // Bucket holds the parts (interior + assigned walls) for each room.
        var partsByCellId = new Dictionary<int, List<MeshData>>(plan.Rooms.Count);
        foreach (var room in plan.Rooms)
        {
            var interior = BuildInteriorFor(room, wallThickness, wallHeight);
            partsByCellId[room.Id] = new List<MeshData> { interior };
        }

        // Phase 2: assign each adjacency's wall to its owner room.
        foreach (var adj in plan.Adjacencies)
        {
            int ownerId = ResolveWallOwner(adj);
            var wall = _wallBuilder.Build(adj, wallThickness, wallHeight);
            partsByCellId[ownerId].Add(wall);
        }

        // Phase 3: combine each room's parts.
        var result = new AssembledRoomMesh[plan.Rooms.Count];
        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            var room = plan.Rooms[i];
            var combined = MeshData.Combine(partsByCellId[room.Id]);
            result[i] = new AssembledRoomMesh(room, combined);
        }
        return result;
    }

    MeshData BuildInteriorFor(Room room, float t, float h)
    {
        return room.Shape switch
        {
            RectangleShape => _rectInterior.Build(room, t, h),
            _ => throw new InvalidOperationException(
                $"MultiRoomEnvironmentMeshAssembler does not yet support room shape '{room.Shape.GetType().Name}'. " +
                "Add an interior builder + a dispatch case in BuildInteriorFor."),
        };
    }

    /// <summary>
    /// The room whose mesh will contain the wall geometry for this adjacency.
    /// • Outer adjacency: RoomA (the only room).
    /// • Internal adjacency: lower-id room.
    /// </summary>
    static int ResolveWallOwner(Adjacency adj)
    {
        if (adj.IsOuter) return adj.RoomA.Id;
        return adj.RoomA.Id < adj.RoomB!.Id ? adj.RoomA.Id : adj.RoomB.Id;
    }
}
