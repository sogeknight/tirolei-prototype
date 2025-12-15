using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class ProceduralRoomGenerator : MonoBehaviour
{
    [Header("Grid donde se crearán las salas")]
    public Grid targetGrid;

    [Header("Sala")]
    public int roomWidth = 30;
    public int roomHeight = 20;
    public int roomCount = 3;
    public int roomSpacing = 4;

    [Header("Bordes básicos")]
    public bool useFloor = true;
    public bool useCeiling = true;
    public bool useLeftWall = true;
    public bool useRightWall = true;

    [Header("Ruido tipo 'cueva'")]
    public bool useCaveNoise = true;
    [Range(0, 100)] public int randomFillPercent = 45;
    [Range(0, 10)] public int smoothIterations = 5;

    // ============================================================
    //  RUIDO POR LADO FÍSICO (DIRECCIONES ABSOLUTAS)
    // ============================================================
    public enum AxisFill
    {
        None,       // no aplica ruido en ese lado
        TowardA,    // hacia arriba / hacia derecha (según lado)
        TowardB,    // hacia abajo / hacia izquierda (según lado)
        Both        // ambos sentidos
    }

    [Header("Ruido por lado físico (dirección absoluta)")]
    [Tooltip("Suelo: TowardA=ARRIBA, TowardB=ABAJO")]
    public AxisFill floorFill = AxisFill.TowardA;
    [Range(0, 40)] public int floorDepth = 6;

    [Tooltip("Techo: TowardA=ABAJO, TowardB=ARRIBA")]
    public AxisFill ceilingFill = AxisFill.None;
    [Range(0, 40)] public int ceilingDepth = 6;

    [Tooltip("Pared izquierda: TowardA=DERECHA, TowardB=IZQUIERDA")]
    public AxisFill leftWallFill = AxisFill.None;
    [Range(0, 40)] public int leftWallDepth = 6;

    [Tooltip("Pared derecha: TowardA=IZQUIERDA, TowardB=DERECHA")]
    public AxisFill rightWallFill = AxisFill.None;
    [Range(0, 40)] public int rightWallDepth = 6;

    [Header("Aberturas laterales (por sala)")]
    [Range(0, 8)] public int openingsPerSide = 1;
    [Range(1, 10)] public int openingHeight = 3;
    [Range(1, 20)] public int openingDepth = 2;

    [Header("Random")]
    public bool useRandomSeed = true;
    public string seed = "tirolei";

    [Header("Tiles")]
    public TileBase groundTile;

    [Header("Cámara (opcional)")]
    public Camera targetCamera;
    public float cameraPadding = 2f;

    [Header("Ruido de PLATAFORMAS internas")]
    public bool usePlatformNoise = false;
    [Range(0, 100)] public int platformNoisePercent = 15;
    [Range(1, 10)] public int platformMinLength = 2;
    [Range(1, 10)] public int platformMaxLength = 5;

    private System.Random prng;

    // ========= BOTONES DE INSPECTOR =========

    [ContextMenu("Generate Rooms (separate Tilemaps)")]
    public void GenerateRoomsAdditive()
    {
        if (!CheckSetup()) return;
        InitPrng();

        float startOffsetX = GetRightmostRoomEndX();

        for (int i = 0; i < roomCount; i++)
        {
            float offsetX = startOffsetX + i * (roomWidth + roomSpacing);
            CreateAndFillRoom(offsetX);
        }

        AutoCenterCameraOnAllRooms();
    }

    [ContextMenu("Clear All Generated Rooms")]
    public void ClearAllGeneratedRooms()
    {
        if (targetGrid == null) return;

        var children = targetGrid.GetComponentsInChildren<Transform>(true);
        foreach (var t in children)
        {
            if (t == targetGrid.transform) continue;
            if (!t.name.StartsWith("Room_")) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(t.gameObject);
            else Destroy(t.gameObject);
#else
            DestroyImmediate(t.gameObject);
#endif
        }
    }

    // ========= LÓGICA DE GENERACIÓN =========

    private bool CheckSetup()
    {
        if (targetGrid == null)
        {
            Debug.LogError("[ProceduralRoomGenerator] Asigna targetGrid (un Grid en la escena).");
            return false;
        }

        if (groundTile == null)
        {
            Debug.LogError("[ProceduralRoomGenerator] Asigna groundTile.");
            return false;
        }

        if (roomWidth <= 0 || roomHeight <= 0 || roomCount <= 0)
        {
            Debug.LogError("[ProceduralRoomGenerator] roomWidth, roomHeight y roomCount deben ser > 0.");
            return false;
        }

        return true;
    }

    private void InitPrng()
    {
        prng = useRandomSeed ? new System.Random() : new System.Random(seed.GetHashCode());
    }

    /// <summary>
    /// Devuelve el X local donde termina el Room más a la derecha, para seguir añadiendo sin pisar.
    /// </summary>
    private float GetRightmostRoomEndX()
    {
        float maxEndX = 0f;
        bool foundAny = false;

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>();
        foreach (var tm in tilemaps)
        {
            if (!tm.name.StartsWith("Room_")) continue;
            foundAny = true;

            tm.CompressBounds();
            var b = tm.localBounds;
            float endX = tm.transform.localPosition.x + b.max.x;
            if (endX > maxEndX) maxEndX = endX;
        }

        if (!foundAny) return 0f;
        return maxEndX + roomSpacing;
    }

    private void CreateAndFillRoom(float offsetX)
    {
        int index = GetNextRoomIndex();

        var roomGO = new GameObject($"Room_{index:D2}");
        roomGO.transform.SetParent(targetGrid.transform, false);
        roomGO.transform.localPosition = new Vector3(offsetX, 0f, 0f);

        var tilemap = roomGO.AddComponent<Tilemap>();
        var renderer = roomGO.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 0;

        RoomGenResult result = GenerateSingleRoom(roomWidth, roomHeight);
        DrawRoomToTilemap(result.map, tilemap, result.drawOffsetX, result.drawOffsetY);
    }

    private int GetNextRoomIndex()
    {
        int maxIndex = -1;

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>(true);
        foreach (var tm in tilemaps)
        {
            if (!tm.name.StartsWith("Room_")) continue;

            string suffix = tm.name.Substring("Room_".Length);
            if (int.TryParse(suffix, out int idx))
            {
                if (idx > maxIndex) maxIndex = idx;
            }
        }

        return maxIndex + 1;
    }

    // =========================
    //  GENERACIÓN CON MAPA EXTENDIDO (para "hacia fuera")
    // =========================
    private struct RoomGenResult
    {
        public int[,] map;
        public int drawOffsetX; // para centrar el rectángulo original en (0..w-1)
        public int drawOffsetY;
    }

    private RoomGenResult GenerateSingleRoom(int width, int height)
    {
        // Depths seguros
        int fD = Mathf.Max(0, floorDepth);
        int cD = Mathf.Max(0, ceilingDepth);
        int lD = Mathf.Max(0, leftWallDepth);
        int rD = Mathf.Max(0, rightWallDepth);

        // Cuánto necesitamos expandir fuera del rectángulo original
        int extraDown = (floorFill == AxisFill.TowardB || floorFill == AxisFill.Both) ? fD : 0;   // suelo hacia ABAJO
        int extraUp   = (ceilingFill == AxisFill.TowardB || ceilingFill == AxisFill.Both) ? cD : 0; // techo hacia ARRIBA
        int extraLeft = (leftWallFill == AxisFill.TowardB || leftWallFill == AxisFill.Both) ? lD : 0; // pared izq hacia IZQUIERDA
        int extraRight= (rightWallFill == AxisFill.TowardB || rightWallFill == AxisFill.Both) ? rD : 0; // pared der hacia DERECHA

        int extW = width + extraLeft + extraRight;
        int extH = height + extraDown + extraUp;

        // Offset del rectángulo original dentro del mapa extendido
        int ox = extraLeft;
        int oy = extraDown;

        int[,] map = new int[extW, extH];
        bool[,] locked = new bool[extW, extH];

        // 1) todo vacío
        for (int x = 0; x < extW; x++)
            for (int y = 0; y < extH; y++)
                map[x, y] = 0;

        // Coordenadas del rectángulo original dentro del mapa extendido
        int roomMinX = ox;
        int roomMaxX = ox + width - 1;
        int roomMinY = oy;
        int roomMaxY = oy + height - 1;

        // 2) bordes base (en el rectángulo original)
        if (useFloor)
        {
            for (int x = roomMinX; x <= roomMaxX; x++)
            {
                map[x, roomMinY] = 1;
                locked[x, roomMinY] = true;
            }
        }

        if (useCeiling)
        {
            for (int x = roomMinX; x <= roomMaxX; x++)
            {
                map[x, roomMaxY] = 1;
                locked[x, roomMaxY] = true;
            }
        }

        if (useLeftWall)
        {
            for (int y = roomMinY; y <= roomMaxY; y++)
            {
                map[roomMinX, y] = 1;
                locked[roomMinX, y] = true;
            }
        }

        if (useRightWall)
        {
            for (int y = roomMinY; y <= roomMaxY; y++)
            {
                map[roomMaxX, y] = 1;
                locked[roomMaxX, y] = true;
            }
        }

        // 3) ruido dirigido por lado físico
        if (useCaveNoise && randomFillPercent > 0 && extW >= 3 && extH >= 3)
        {
            ApplySideNoise(map, locked, extW, extH, roomMinX, roomMaxX, roomMinY, roomMaxY);

            for (int i = 0; i < smoothIterations; i++)
                map = SmoothMapWithLocks(map, locked, extW, extH);
        }

        // 4) openings laterales (si hay paredes activas) -> siempre “hacia dentro del rectángulo original”
        CarveSideOpenings(map, width, height, roomMinX, roomMaxX, roomMinY, roomMaxY);

        // 5) plataformas internas (solo dentro del rectángulo original)
        if (usePlatformNoise)
            AddPlatformNoise(map, width, height, roomMinX, roomMaxX, roomMinY, roomMaxY);

        // Para dibujar: queremos que el rectángulo original empiece en (0,0) del tilemap.
        // Eso implica restar ox,oy -> coords negativas para lo “fuera”.
        return new RoomGenResult
        {
            map = map,
            drawOffsetX = -ox,
            drawOffsetY = -oy
        };
    }

    private void ApplySideNoise(int[,] map, bool[,] locked, int extW, int extH,
        int roomMinX, int roomMaxX, int roomMinY, int roomMaxY)
    {
        // Helper: set cell si no está locked
        void TrySet(int x, int y)
        {
            if (x < 0 || x >= extW || y < 0 || y >= extH) return;
            if (locked[x, y]) return;
            if (map[x, y] == 1) return;
            map[x, y] = (prng.Next(0, 100) < randomFillPercent) ? 1 : 0;
        }

        // SUELO
        // TowardA=ARRIBA (dentro del rectángulo): desde roomMinY+1 hacia arriba depth
        if (floorFill == AxisFill.TowardA || floorFill == AxisFill.Both)
        {
            int y0 = roomMinY + 1;
            int y1 = Mathf.Min(roomMinY + floorDepth, roomMaxY - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                    TrySet(x, y);
        }
        // TowardB=ABAJO (fuera): desde roomMinY-1 hacia abajo depth
        if (floorFill == AxisFill.TowardB || floorFill == AxisFill.Both)
        {
            int y0 = roomMinY - 1;
            int y1 = roomMinY - floorDepth;
            for (int y = y0; y >= y1; y--)
                for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                    TrySet(x, y);
        }

        // TECHO
        // TowardA=ABAJO (dentro): desde roomMaxY-1 hacia abajo depth
        if (ceilingFill == AxisFill.TowardA || ceilingFill == AxisFill.Both)
        {
            int y0 = roomMaxY - 1;
            int y1 = Mathf.Max(roomMaxY - ceilingDepth, roomMinY + 1);
            for (int y = y0; y >= y1; y--)
                for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                    TrySet(x, y);
        }
        // TowardB=ARRIBA (fuera): desde roomMaxY+1 hacia arriba depth
        if (ceilingFill == AxisFill.TowardB || ceilingFill == AxisFill.Both)
        {
            int y0 = roomMaxY + 1;
            int y1 = roomMaxY + ceilingDepth;
            for (int y = y0; y <= y1; y++)
                for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                    TrySet(x, y);
        }

        // PARED IZQUIERDA
        // TowardA=DERECHA (dentro): desde roomMinX+1 hacia derecha depth
        if (leftWallFill == AxisFill.TowardA || leftWallFill == AxisFill.Both)
        {
            int x0 = roomMinX + 1;
            int x1 = Mathf.Min(roomMinX + leftWallDepth, roomMaxX - 1);
            for (int x = x0; x <= x1; x++)
                for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                    TrySet(x, y);
        }
        // TowardB=IZQUIERDA (fuera): desde roomMinX-1 hacia izquierda depth
        if (leftWallFill == AxisFill.TowardB || leftWallFill == AxisFill.Both)
        {
            int x0 = roomMinX - 1;
            int x1 = roomMinX - leftWallDepth;
            for (int x = x0; x >= x1; x--)
                for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                    TrySet(x, y);
        }

        // PARED DERECHA
        // TowardA=IZQUIERDA (dentro): desde roomMaxX-1 hacia izquierda depth
        if (rightWallFill == AxisFill.TowardA || rightWallFill == AxisFill.Both)
        {
            int x0 = roomMaxX - 1;
            int x1 = Mathf.Max(roomMaxX - rightWallDepth, roomMinX + 1);
            for (int x = x0; x >= x1; x--)
                for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                    TrySet(x, y);
        }
        // TowardB=DERECHA (fuera): desde roomMaxX+1 hacia derecha depth
        if (rightWallFill == AxisFill.TowardB || rightWallFill == AxisFill.Both)
        {
            int x0 = roomMaxX + 1;
            int x1 = roomMaxX + rightWallDepth;
            for (int x = x0; x <= x1; x++)
                for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                    TrySet(x, y);
        }
    }

    private int[,] SmoothMapWithLocks(int[,] map, bool[,] locked, int w, int h)
    {
        int[,] newMap = new int[w, h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (locked[x, y])
                {
                    newMap[x, y] = map[x, y];
                    continue;
                }

                int neighbours = GetSurroundingCount(map, w, h, x, y);

                if (neighbours > 4) newMap[x, y] = 1;
                else if (neighbours < 4) newMap[x, y] = 0;
                else newMap[x, y] = map[x, y];
            }
        }

        return newMap;
    }

    private int GetSurroundingCount(int[,] map, int w, int h, int cx, int cy)
    {
        int count = 0;

        for (int x = cx - 1; x <= cx + 1; x++)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (x == cx && y == cy) continue;

                if (x < 0 || x >= w || y < 0 || y >= h)
                {
                    // Fuera del array -> vacío (0)
                    continue;
                }

                count += map[x, y];
            }
        }

        return count;
    }

    // ============================================================
    //  OPENINGS (solo paredes) hacia dentro del rectángulo original
    // ============================================================
    private void CarveSideOpenings(int[,] map, int roomW, int roomH,
        int roomMinX, int roomMaxX, int roomMinY, int roomMaxY)
    {
        if (openingsPerSide <= 0 || openingHeight <= 0) return;
        bool anySideActive = useLeftWall || useRightWall;
        if (!anySideActive) return;

        int interiorMinY = roomMinY + 1;
        int interiorMaxY = roomMaxY - 1;
        int interiorHeight = interiorMaxY - interiorMinY + 1;
        if (interiorHeight <= 0) return;

        int effectiveHeight = Mathf.Clamp(openingHeight, 1, interiorHeight);
        int effectiveDepth = Mathf.Clamp(openingDepth, 1, Mathf.Max(1, roomW / 2));

        if (useLeftWall)
            CarveOpeningsOnSide(map, roomMinX, isLeft: true,
                interiorMinY, interiorMaxY, effectiveHeight, effectiveDepth);

        if (useRightWall)
            CarveOpeningsOnSide(map, roomMaxX, isLeft: false,
                interiorMinY, interiorMaxY, effectiveHeight, effectiveDepth);
    }

    private void CarveOpeningsOnSide(int[,] map, int wallX, bool isLeft,
        int interiorMinY, int interiorMaxY, int effectiveHeight, int effectiveDepth)
    {
        float interiorHeight = interiorMaxY - interiorMinY + 1;
        float segmentSize = interiorHeight / (openingsPerSide + 1);

        for (int i = 0; i < openingsPerSide; i++)
        {
            float centerYFloat = interiorMinY + (i + 1) * segmentSize;
            int centerY = Mathf.RoundToInt(centerYFloat);

            int startY = centerY - effectiveHeight / 2;
            startY = Mathf.Clamp(startY, interiorMinY, interiorMaxY - effectiveHeight + 1);
            int endY = startY + effectiveHeight - 1;

            for (int y = startY; y <= endY; y++)
            {
                if (isLeft)
                {
                    // pared izq -> limpiar desde wallX hacia la derecha
                    for (int dx = 0; dx < effectiveDepth; dx++)
                        map[wallX + dx, y] = 0;
                }
                else
                {
                    // pared der -> limpiar desde wallX hacia la izquierda
                    for (int dx = 0; dx < effectiveDepth; dx++)
                        map[wallX - dx, y] = 0;
                }
            }
        }
    }

    // ============================================================
    //  PLATFORM NOISE (solo dentro del rectángulo original)
    // ============================================================
    private void AddPlatformNoise(int[,] map, int roomW, int roomH,
        int roomMinX, int roomMaxX, int roomMinY, int roomMaxY)
    {
        int minY = roomMinY + 2;
        int maxY = roomMaxY - 2;
        if (maxY <= minY) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
            {
                if (map[x, y] != 0) continue;
                if (prng.Next(0, 100) >= platformNoisePercent) continue;

                int length = prng.Next(platformMinLength, platformMaxLength + 1);
                int dir = (prng.NextDouble() < 0.5) ? -1 : 1;

                int startX = x;
                for (int i = 0; i < length; i++)
                {
                    int px = startX + i * dir;

                    if (px <= roomMinX || px >= roomMaxX) break;
                    if (map[px, y] != 0) break;

                    if (map[px, y - 1] == 0 && map[px, y + 1] == 0)
                        map[px, y] = 1;
                }
            }
        }
    }

    // ============================================================
    //  DIBUJO (con offset para permitir negativos)
    // ============================================================
    private void DrawRoomToTilemap(int[,] map, Tilemap tilemap, int offsetX, int offsetY)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);

        tilemap.ClearAllTiles();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (map[x, y] != 1) continue;

                int tx = x + offsetX;
                int ty = y + offsetY;
                tilemap.SetTile(new Vector3Int(tx, ty, 0), groundTile);
            }
        }
    }

    // ============================================================
    //  CÁMARA
    // ============================================================
    private void AutoCenterCameraOnAllRooms()
    {
        if (targetCamera == null || targetGrid == null) return;

        bool hasBounds = false;
        Bounds totalBounds = new Bounds();

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>();
        foreach (var tm in tilemaps)
        {
            if (!tm.name.StartsWith("Room_")) continue;

            tm.CompressBounds();
            var b = tm.localBounds;
            b.center += tm.transform.position;

            if (!hasBounds)
            {
                totalBounds = b;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(b);
            }
        }

        if (!hasBounds) return;

        Vector3 center = totalBounds.center;
        Vector3 extents = totalBounds.extents;

        targetCamera.orthographic = true;
        targetCamera.transform.position = new Vector3(center.x, center.y, -10f);

        float vertExtent = extents.y;
        float horizExtent = extents.x / Mathf.Max(targetCamera.aspect, 0.0001f);
        float neededSize = Mathf.Max(vertExtent, horizExtent) + cameraPadding;
        targetCamera.orthographicSize = neededSize;
    }
}
