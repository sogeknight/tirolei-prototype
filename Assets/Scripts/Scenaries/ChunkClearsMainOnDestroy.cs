using UnityEngine;
using UnityEngine.Tilemaps;

public class ChunkClearsMainOnDestroy : MonoBehaviour
{
    [Header("Refs")]
    public Tilemap mainTilemap;

    [Header("Main-rect to clear (cell coords in MAIN)")]
    public Vector3Int mainMin;   // inclusive
    public Vector3Int mainMax;   // inclusive

    [Header("Debug")]
    public bool debugLog = false;

    private void OnDestroy()
    {
        if (mainTilemap == null) return;

        // Limpia tiles del MAIN en el rectángulo asignado a este chunk
        for (int x = mainMin.x; x <= mainMax.x; x++)
        for (int y = mainMin.y; y <= mainMax.y; y++)
        {
            mainTilemap.SetTile(new Vector3Int(x, y, 0), null);
        }

        if (debugLog)
        {
            Debug.Log($"[ChunkClearsMainOnDestroy] Cleared MAIN rect {mainMin} -> {mainMax} (chunk {name})");
        }
    }
}
