namespace OpenApparatus.Topology;

/// <summary>
/// Classification of a room placed by a tiling generator. Currently used by
/// <c>GridDominoGenerator</c> to distinguish single-tile rooms from 1×2 dominoes.
/// </summary>
public enum RoomType
{
    Square,
    Rectangle,
}
