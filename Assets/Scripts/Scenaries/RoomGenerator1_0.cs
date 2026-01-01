using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class RoomGenerator1_0 : MonoBehaviour
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

    public enum AxisFill { None, TowardA, TowardB, Both }

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

    [Header("Colisión / Layer + Tag")]
    [Tooltip("Layer que se asigna a Room_XX y todos sus hijos (Chunks incluidos). Ej: Ground")]
    public string roomLayerName = "Ground";
    [Tooltip("Tag que se asigna a Room_XX y todos sus hijos (Chunks incluidos). Debe existir en Unity: Tags & Layers.")]
    public string roomTagName = "Ground";

    [Header("Infraestructura: Tier aplicado a TODA la sala (VISUAL)")]
    public bool useWorldMaterialForRoom = true; // solo se usa en modo NO-subtiles
    public bool baseSolid_NoSystem = false;     // si true => blanco y sin WorldMaterial
    public MaterialTier roomTier = MaterialTier.MaterialTier_I_Fragile;

    [Header("HP por Tier (si no es base sólido)")]
    public float hpFragile = 3f;
    public float hpWeak = 8f;
    public float hpStructural = 18f;
    public float hpCore = 35f;
    public float hpSeal = 999999f;

    [Header("Color por Tier (visual)")]
    public Color colorBaseSolid = Color.white;
    public Color colorFragile = new Color(0.7f, 1f, 1f, 1f);
    public Color colorWeak = new Color(0.6f, 1f, 0.6f, 1f);
    public Color colorStructural = new Color(1f, 0.85f, 0.4f, 1f);
    public Color colorCore = new Color(1f, 0.5f, 0.5f, 1f);
    public Color colorSeal = new Color(0.85f, 0.6f, 1f, 1f);

    [Header("Subtiles (colisión/rotura granular)")]
    public bool useSubtilesForBreakableTiers = true;

    [Tooltip("N subtiles por lado. 1 = normal. 2 = 2x2. 4 = 4x4. 8 = 8x8.")]
    [Range(1, 20)] public int subtilesPerTile = 8;

    [Tooltip("Tile que se usa para los subtiles. Si null, usa groundTile.")]
    public TileBase subtileGroundTile;

    [Header("Chunking EN SUBTILES (trozos pequeños de verdad)")]
    [Tooltip("Tamaño del chunk en SUBTILES (no en tiles grandes). Ej: 4 => chunk = 4x4 subtiles (medio tile si subtilesPerTile=8).")]
    [Range(1, 64)] public int chunkSubtilesPerSide = 4;

    [Tooltip("Si true, la capa Sub se renderiza para VER la rotura. Si false, solo colisión.")]
    public bool renderSubtiles = true;

    [Tooltip("Sorting order de los subtiles (sube si no lo ves).")]
    public int subtilesSortingOrder = 5;

    [Header("Debug")]
    public bool debugWorldMaterial = false;

    private System.Random prng;

    [ContextMenu("Generate Rooms 1.0 (tiered)")]
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
    }

    [ContextMenu("Clear All Generated Rooms (1.0)")]
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

    private bool CheckSetup()
    {
        if (targetGrid == null)
        {
            Debug.LogError("[RoomGenerator1_0] Asigna targetGrid (un Grid en la escena).");
            return false;
        }

        if (groundTile == null)
        {
            Debug.LogError("[RoomGenerator1_0] Asigna groundTile.");
            return false;
        }

        if (roomWidth <= 0 || roomHeight <= 0 || roomCount <= 0)
        {
            Debug.LogError("[RoomGenerator1_0] roomWidth, roomHeight y roomCount deben ser > 0.");
            return false;
        }

        return true;
    }

    private void InitPrng()
    {
        prng = useRandomSeed ? new System.Random() : new System.Random(seed.GetHashCode());
    }

    private float GetRightmostRoomEndX()
    {
        float maxEndX = 0f;
        bool foundAny = false;

        var tilemaps = targetGrid.GetComponentsInChildren<Tilemap>(true);
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

        // Layer + Tag (sala y todo lo que cuelgue)
        int layer = LayerMask.NameToLayer(roomLayerName);
        if (layer >= 0) roomGO.layer = layer;
        ApplyTagSafe(roomGO, roomTagName);

        // ===== MAIN (visual macro) =====
        var mainGO = new GameObject("Tilemap_Main");
        mainGO.transform.SetParent(roomGO.transform, false);
        mainGO.layer = roomGO.layer;
        ApplyTagSafe(mainGO, roomTagName);

        var mainTilemap = mainGO.AddComponent<Tilemap>();
        var mainRenderer = mainGO.AddComponent<TilemapRenderer>();
        mainRenderer.sortingOrder = 0;

        RoomGenResult result = GenerateSingleRoom(roomWidth, roomHeight);
        DrawRoomToTilemap(result.map, mainTilemap, result.drawOffsetX, result.drawOffsetY);

        // Color (solo afecta si el renderer está on)
        ApplyVisualInfrastructure(mainTilemap);

        bool shouldUseSub =
            useSubtilesForBreakableTiers &&
            !baseSolid_NoSystem &&
            subtilesPerTile > 1 &&
            roomTier != MaterialTier.MaterialTier_S_Seal;

        if (shouldUseSub)
        {
            // En modo SUB: el MAIN NO debe ser “la verdad” visual (si lo dejas, no verás la rotura real).
            mainRenderer.enabled = false;

            // NO pongas WorldMaterial en Room (si lo pones, romperás toda la sala)
            RemoveWorldMaterialIfAny(roomGO);

            CreateSubtileChunks_BySubtileGrid(roomGO, result.map, result.drawOffsetX, result.drawOffsetY);
        }
        else
        {
            // Modo normal (sin subtiles): colisión en MAIN
            if (!baseSolid_NoSystem && useWorldMaterialForRoom)
                EnsureWorldMaterialOn(roomGO);

            var rb2d = mainGO.AddComponent<Rigidbody2D>();
            rb2d.bodyType = RigidbodyType2D.Static;

            var tmCol = mainGO.AddComponent<TilemapCollider2D>();
            tmCol.usedByComposite = true;

            var comp = mainGO.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }
    }

    private void ApplyVisualInfrastructure(Tilemap mainVisual)
    {
        mainVisual.color = GetRoomColor();
    }

    private Color GetRoomColor()
    {
        if (baseSolid_NoSystem) return colorBaseSolid;
        return TierToColor(roomTier);
    }

    private void RemoveWorldMaterialIfAny(GameObject go)
    {
        var existing = go.GetComponent<WorldMaterial>();
        if (existing == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(existing);
        else Destroy(existing);
#else
        Destroy(existing);
#endif
    }

    private void EnsureWorldMaterialOn(GameObject go)
    {
        var wm = go.GetComponent<WorldMaterial>();
        if (wm == null) wm = go.AddComponent<WorldMaterial>();

        wm.tier = roomTier;
        wm.structuralHP = TierToHP(roomTier);
        wm.indestructible = (roomTier == MaterialTier.MaterialTier_S_Seal);
        wm.debugLogs = debugWorldMaterial;
    }

    /// <summary>
    /// CHUNKING REAL: en coordenadas de SUBTILES.
    /// Chunk pequeño = se rompe pequeño.
    /// Y AHORA: cada chunk se colorea para que veas la infraestructura.
    /// </summary>
    private void CreateSubtileChunks_BySubtileGrid(GameObject roomGO, int[,] map, int offsetX, int offsetY)
    {
        int n = Mathf.Max(1, subtilesPerTile);
        int chunkS = Mathf.Max(1, chunkSubtilesPerSide);
        TileBase t = (subtileGroundTile != null) ? subtileGroundTile : groundTile;

        // SubGrid con cellSize reducido
        var subGridGO = new GameObject("SubGrid");
        subGridGO.transform.SetParent(roomGO.transform, false);
        subGridGO.layer = roomGO.layer;
        ApplyTagSafe(subGridGO, roomTagName);

        var subGrid = subGridGO.AddComponent<Grid>();
        Vector3 parentCell = targetGrid.cellSize;
        subGrid.cellSize = new Vector3(parentCell.x / n, parentCell.y / n, parentCell.z);

        // Agrupar TODOS los subtiles por chunkKey (cx,cy) en espacio de subtiles
        Dictionary<(int cx, int cy), List<Vector3Int>> chunkCells = new Dictionary<(int, int), List<Vector3Int>>();

        int w = map.GetLength(0);
        int h = map.GetLength(1);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (map[x, y] != 1) continue;

            int tx = x + offsetX;
            int ty = y + offsetY;

            int sx0 = tx * n;
            int sy0 = ty * n;

            for (int sx = 0; sx < n; sx++)
            for (int sy = 0; sy < n; sy++)
            {
                int sxx = sx0 + sx;
                int syy = sy0 + sy;

                int cx = FloorDiv(sxx, chunkS);
                int cy = FloorDiv(syy, chunkS);

                var key = (cx, cy);
                if (!chunkCells.TryGetValue(key, out var list))
                {
                    list = new List<Vector3Int>(128);
                    chunkCells[key] = list;
                }

                list.Add(new Vector3Int(sxx, syy, 0));
            }
        }

        Color chunkColor = GetRoomColor();

        // Crear un GO por chunk y pintar SOLO sus subtiles
        foreach (var kv in chunkCells)
        {
            var key = kv.Key;
            var cells = kv.Value;

            var chunkGO = new GameObject($"Chunk_{key.cx}_{key.cy}");
            chunkGO.transform.SetParent(subGridGO.transform, false);

            // Layer + Tag
            chunkGO.layer = roomGO.layer;
            ApplyTagSafe(chunkGO, roomTagName);

            var tm = chunkGO.AddComponent<Tilemap>();
            tm.color = chunkColor; // <-- AQUÍ ESTÁ EL COLOR DE INFRAESTRUCTURA EN SUBTILES

            var tr = chunkGO.AddComponent<TilemapRenderer>();
            tr.enabled = renderSubtiles;
            tr.sortingOrder = subtilesSortingOrder;

            var rb2d = chunkGO.AddComponent<Rigidbody2D>();
            rb2d.bodyType = RigidbodyType2D.Static;

            var tmCol = chunkGO.AddComponent<TilemapCollider2D>();
            tmCol.usedByComposite = true;

            var comp = chunkGO.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;

            // WorldMaterial en el CHUNK (esto hace que se rompa por trozos)
            var wm = chunkGO.AddComponent<WorldMaterial>();
            wm.tier = roomTier;
            wm.structuralHP = TierToHP(roomTier);
            wm.indestructible = (roomTier == MaterialTier.MaterialTier_S_Seal);
            wm.debugLogs = debugWorldMaterial;

            tm.ClearAllTiles();
            for (int i = 0; i < cells.Count; i++)
                tm.SetTile(cells[i], t);
        }
    }

    // División entera hacia abajo (soporta negativos)
    private int FloorDiv(int a, int b)
    {
        if (b == 0) return 0;
        int q = a / b;
        int r = a % b;
        if (r != 0 && ((r > 0) != (b > 0))) q--;
        return q;
    }

    // Asigna tag sin reventar si no existe (Unity peta si el tag no está creado)
    private void ApplyTagSafe(GameObject go, string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;

#if UNITY_EDITOR
        // Validación de tags en editor
        try
        {
            bool exists = false;
            foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (t == tagName) { exists = true; break; }
            }

            if (!exists)
            {
                Debug.LogError($"[RoomGenerator1_0] El Tag '{tagName}' NO existe. Créalo en Project Settings > Tags and Layers.");
                return;
            }
        }
        catch
        {
            // Si por lo que sea no podemos consultar tags, intentamos asignar igualmente
        }
#endif

        try { go.tag = tagName; }
        catch
        {
            Debug.LogError($"[RoomGenerator1_0] No pude asignar Tag '{tagName}'. ¿Existe en Tags and Layers?");
        }
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
                if (idx > maxIndex) maxIndex = idx;
        }

        return maxIndex + 1;
    }

    private float TierToHP(MaterialTier tier)
    {
        switch (tier)
        {
            case MaterialTier.MaterialTier_I_Fragile: return hpFragile;
            case MaterialTier.MaterialTier_II_Weak: return hpWeak;
            case MaterialTier.MaterialTier_III_Structural: return hpStructural;
            case MaterialTier.MaterialTier_IV_Core: return hpCore;
            case MaterialTier.MaterialTier_S_Seal: return hpSeal;
            default: return hpStructural;
        }
    }

    private Color TierToColor(MaterialTier tier)
    {
        switch (tier)
        {
            case MaterialTier.MaterialTier_I_Fragile: return colorFragile;
            case MaterialTier.MaterialTier_II_Weak: return colorWeak;
            case MaterialTier.MaterialTier_III_Structural: return colorStructural;
            case MaterialTier.MaterialTier_IV_Core: return colorCore;
            case MaterialTier.MaterialTier_S_Seal: return colorSeal;
            default: return Color.white;
        }
    }

    // =========================
    //  GENERACIÓN
    // =========================
    private struct RoomGenResult
    {
        public int[,] map;
        public int drawOffsetX;
        public int drawOffsetY;
    }

    private RoomGenResult GenerateSingleRoom(int width, int height)
    {
        int fD = Mathf.Max(0, floorDepth);
        int cD = Mathf.Max(0, ceilingDepth);
        int lD = Mathf.Max(0, leftWallDepth);
        int rD = Mathf.Max(0, rightWallDepth);

        int extraDown = (floorFill == AxisFill.TowardB || floorFill == AxisFill.Both) ? fD : 0;
        int extraUp = (ceilingFill == AxisFill.TowardB || ceilingFill == AxisFill.Both) ? cD : 0;
        int extraLeft = (leftWallFill == AxisFill.TowardB || leftWallFill == AxisFill.Both) ? lD : 0;
        int extraRight = (rightWallFill == AxisFill.TowardB || rightWallFill == AxisFill.Both) ? rD : 0;

        int extW = width + extraLeft + extraRight;
        int extH = height + extraDown + extraUp;

        int ox = extraLeft;
        int oy = extraDown;

        int[,] map = new int[extW, extH];
        bool[,] locked = new bool[extW, extH];

        for (int x = 0; x < extW; x++)
        for (int y = 0; y < extH; y++)
            map[x, y] = 0;

        int roomMinX = ox;
        int roomMaxX = ox + width - 1;
        int roomMinY = oy;
        int roomMaxY = oy + height - 1;

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

        if (useCaveNoise && randomFillPercent > 0 && extW >= 3 && extH >= 3)
        {
            ApplySideNoise(map, locked, extW, extH, roomMinX, roomMaxX, roomMinY, roomMaxY);

            for (int i = 0; i < smoothIterations; i++)
                map = SmoothMapWithLocks(map, locked, extW, extH);
        }

        CarveSideOpenings(map, width, height, roomMinX, roomMaxX, roomMinY, roomMaxY);

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
        void TrySet(int x, int y)
        {
            if (x < 0 || x >= extW || y < 0 || y >= extH) return;
            if (locked[x, y]) return;
            if (map[x, y] == 1) return;
            map[x, y] = (prng.Next(0, 100) < randomFillPercent) ? 1 : 0;
        }

        if (floorFill == AxisFill.TowardA || floorFill == AxisFill.Both)
        {
            int y0 = roomMinY + 1;
            int y1 = Mathf.Min(roomMinY + floorDepth, roomMaxY - 1);
            for (int y = y0; y <= y1; y++)
            for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                TrySet(x, y);
        }

        if (floorFill == AxisFill.TowardB || floorFill == AxisFill.Both)
        {
            int y0 = roomMinY - 1;
            int y1 = roomMinY - floorDepth;
            for (int y = y0; y >= y1; y--)
            for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                TrySet(x, y);
        }

        if (ceilingFill == AxisFill.TowardA || ceilingFill == AxisFill.Both)
        {
            int y0 = roomMaxY - 1;
            int y1 = Mathf.Max(roomMaxY - ceilingDepth, roomMinY + 1);
            for (int y = y0; y >= y1; y--)
            for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                TrySet(x, y);
        }

        if (ceilingFill == AxisFill.TowardB || ceilingFill == AxisFill.Both)
        {
            int y0 = roomMaxY + 1;
            int y1 = roomMaxY + ceilingDepth;
            for (int y = y0; y <= y1; y++)
            for (int x = roomMinX + 1; x <= roomMaxX - 1; x++)
                TrySet(x, y);
        }

        if (leftWallFill == AxisFill.TowardA || leftWallFill == AxisFill.Both)
        {
            int x0 = roomMinX + 1;
            int x1 = Mathf.Min(roomMinX + leftWallDepth, roomMaxX - 1);
            for (int x = x0; x <= x1; x++)
            for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                TrySet(x, y);
        }

        if (leftWallFill == AxisFill.TowardB || leftWallFill == AxisFill.Both)
        {
            int x0 = roomMinX - 1;
            int x1 = roomMinX - leftWallDepth;
            for (int x = x0; x >= x1; x--)
            for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                TrySet(x, y);
        }

        if (rightWallFill == AxisFill.TowardA || rightWallFill == AxisFill.Both)
        {
            int x0 = roomMaxX - 1;
            int x1 = Mathf.Max(roomMaxX - rightWallDepth, roomMinX + 1);
            for (int x = x0; x >= x1; x--)
            for (int y = roomMinY + 1; y <= roomMaxY - 1; y++)
                TrySet(x, y);
        }

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

        return newMap;
    }

    private int GetSurroundingCount(int[,] map, int w, int h, int cx, int cy)
    {
        int count = 0;

        for (int x = cx - 1; x <= cx + 1; x++)
        for (int y = cy - 1; y <= cy + 1; y++)
        {
            if (x == cx && y == cy) continue;
            if (x < 0 || x >= w || y < 0 || y >= h) continue;
            count += map[x, y];
        }

        return count;
    }

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
                    for (int dx = 0; dx < effectiveDepth; dx++)
                        map[wallX + dx, y] = 0;
                }
                else
                {
                    for (int dx = 0; dx < effectiveDepth; dx++)
                        map[wallX - dx, y] = 0;
                }
            }
        }
    }

    private void DrawRoomToTilemap(int[,] map, Tilemap tilemap, int offsetX, int offsetY)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);

        tilemap.ClearAllTiles();

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (map[x, y] != 1) continue;

            int tx = x + offsetX;
            int ty = y + offsetY;
            tilemap.SetTile(new Vector3Int(tx, ty, 0), groundTile);
        }
    }
}
