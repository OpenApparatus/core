namespace OpenApparatus.Topology;

/// <summary>
/// Strategy that mutates the passages on a <see cref="MultiRoomEnvironment"/>'s adjacencies
/// (e.g. picking which closed walls become doorways) to define the floor's
/// connectivity pattern.
/// </summary>
public interface IPassageAssigner
{
    void Assign(MultiRoomEnvironment plan, SeededRandom rng);
}
