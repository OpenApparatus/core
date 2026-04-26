using System;

namespace OpenApparatus.Topology;

/// <summary>
/// A boundary segment between two rooms, or between one room and the outside.
/// Adjacencies are produced by an <see cref="IMultiRoomEnvironmentGenerator"/> (always with
/// <see cref="Passage.Closed"/> initially) and then assigned passages by an
/// <see cref="IPassageAssigner"/> to form the floor plan's connectivity.
/// </summary>
public sealed class Adjacency
{
    public Room RoomA { get; }

    /// <summary>Null when this is an outer-boundary adjacency (room ↔ outside).</summary>
    public Room? RoomB { get; }

    /// <summary>
    /// The boundary segment in world XZ. Direction follows the CCW outline convention:
    /// walking Start → End, RoomA is on the left and RoomB (or outside) is on the right.
    /// </summary>
    public EdgeSegment SharedSegment { get; }

    /// <summary>Mutable — set by the passage assigner. Defaults to <see cref="Passage.Closed.Instance"/>.</summary>
    public Passage Passage { get; set; }

    public Adjacency(Room roomA, Room? roomB, EdgeSegment sharedSegment, Passage? initialPassage = null)
    {
        RoomA = roomA ?? throw new ArgumentNullException(nameof(roomA));
        RoomB = roomB;
        SharedSegment = sharedSegment;
        Passage = initialPassage ?? Passage.Closed.Instance;
    }

    /// <summary>True if this connects two rooms (false for outer-boundary adjacencies).</summary>
    public bool IsInternal => RoomB is not null;

    /// <summary>True if this connects one room to the outside (false for inter-room adjacencies).</summary>
    public bool IsOuter => RoomB is null;

    /// <summary>Returns the other room across this adjacency, or null if outer.</summary>
    public Room? Other(Room room)
    {
        if (room == RoomA) return RoomB;
        if (room == RoomB) return RoomA;
        throw new ArgumentException($"Room #{room.Id} is not part of this adjacency.", nameof(room));
    }

    public override string ToString()
    {
        string b = RoomB is null ? "outside" : $"#{RoomB.Id}";
        return $"#{RoomA.Id} <-> {b} : {Passage} along {SharedSegment}";
    }
}
