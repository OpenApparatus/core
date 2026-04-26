namespace OpenApparatus.Topology;

/// <summary>
/// Strategy that produces a fresh <see cref="FloorPlan"/> from a seeded RNG.
/// Implementations must be deterministic — same instance + same seed must
/// always yield the same plan.
/// </summary>
public interface IFloorPlanGenerator
{
    FloorPlan Generate(SeededRandom rng);
}
