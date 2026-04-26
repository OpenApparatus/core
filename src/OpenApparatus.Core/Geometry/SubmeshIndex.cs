namespace OpenApparatus.Geometry;

/// <summary>
/// Conventional submesh indices used by every room-geometry builder. Engine
/// adapters look these up to assign per-element materials (one floor material,
/// one wall material, one ceiling material per room mesh).
/// </summary>
public static class SubmeshIndex
{
    public const int Floor = 0;
    public const int Walls = 1;
    public const int Ceiling = 2;

    public const int Count = 3;
}
