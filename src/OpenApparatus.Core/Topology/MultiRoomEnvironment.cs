using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenApparatus.Topology;

/// <summary>
/// The output of <see cref="IMultiRoomEnvironmentGenerator.Generate"/>: a set of rooms and the
/// adjacencies between them (and to the outside). This is the engine-agnostic
/// description of a floor plan; the geometry layer turns it into meshes.
/// </summary>
public sealed class MultiRoomEnvironment
{
    public IReadOnlyList<Room> Rooms { get; }
    public IReadOnlyList<Adjacency> Adjacencies { get; }

    public MultiRoomEnvironment(IReadOnlyList<Room> rooms, IReadOnlyList<Adjacency> adjacencies)
    {
        Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        Adjacencies = adjacencies ?? throw new ArgumentNullException(nameof(adjacencies));
    }

    /// <summary>Bounds in world coordinates encompassing every room.</summary>
    public Bounds2D GetWorldBounds()
    {
        if (Rooms.Count == 0)
            throw new InvalidOperationException("Floor plan has no rooms.");
        var b = Rooms[0].GetWorldBounds();
        for (int i = 1; i < Rooms.Count; i++)
            b = Bounds2D.Union(b, Rooms[i].GetWorldBounds());
        return b;
    }

    /// <summary>All adjacencies that touch <paramref name="room"/> (internal or outer).</summary>
    public IEnumerable<Adjacency> GetAdjacenciesOf(Room room)
    {
        for (int i = 0; i < Adjacencies.Count; i++)
        {
            var a = Adjacencies[i];
            if (a.RoomA == room || a.RoomB == room) yield return a;
        }
    }

    /// <summary>All adjacencies between two rooms (excludes outer boundaries).</summary>
    public IEnumerable<Adjacency> GetInternalAdjacencies() =>
        Adjacencies.Where(a => a.IsInternal);

    /// <summary>All adjacencies between a room and the outside.</summary>
    public IEnumerable<Adjacency> GetOuterAdjacencies() =>
        Adjacencies.Where(a => a.IsOuter);
}
