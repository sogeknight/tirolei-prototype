// using System.Collections.Generic;
// using UnityEngine;

// public class PlayerSparkBoostVisuals : MonoBehaviour
// {
//     [Header("Core Ref (auto)")]
//     public PlayerSparkBoostCore core;

//     [Header("Preview Trajectory (AUTO)")]
//     public LineRenderer sparkPreviewLine;
//     public float previewWidth = 0.08f;
//     public int previewSegments = 32;
//     public int previewMaxBounces = 8;
//     public string previewSortingLayer = "Default";
//     public int previewSortingOrder = 9999;

//     [Header("Preview Pickup Marker")]
//     public bool previewMarkNextPickup = true;
//     public LineRenderer pickupPreviewCircle;
//     public float pickupCircleRadius = 0.35f;
//     public int pickupCircleSegments = 32;
//     public float pickupCircleWidth = 0.06f;
//     public string pickupCircleSortingLayer = "Default";
//     public int pickupCircleSortingOrder = 10000;

//     [Header("Ring Preview (Spark Window)")]
//     public LineRenderer sparkRingLine;
//     public float ringRadius = 0.60f;
//     public int ringSegments = 48;
//     public float ringWidth = 0.06f;
//     public string ringSortingLayer = "Default";
//     public int ringSortingOrder = 9998;

//     [Header("DEBUG - Freeze Preview After Dash (permanent)")]
//     public bool debugFreezePreview = true;
//     public bool debugFreezeOnDashStart = true;
//     public int frozenLineSortingOrder = 20000;
//     public float frozenLineWidth = 0.08f;

//     [Header("DEBUG - Hotkeys")]
//     public bool debug = false;
//     public KeyCode debugClearFrozenPreviewKey = KeyCode.F9;

//     private LineRenderer frozenPreviewLine;
//     private bool frozenHasData;

//     private bool wasDashing;

//     private readonly List<Vector3> pts = new List<Vector3>(128);

//     private void Awake()
//     {
//         if (core == null) core = GetComponent<PlayerSparkBoostCore>();
//         if (core == null)
//         {
//             Debug.LogError("[PlayerSparkBoostVisuals] No encuentro PlayerSparkBoostCore en el mismo GameObject.");
//             enabled = false;
//             return;
//         }

//         ConfigurePreviewAuto();
//         ConfigureRingAuto();
//         ConfigurePickupCircleAuto();

//         SetPreviewVisible(false);
//         SetRingVisible(false);
//         SetPickupCircleVisible(false);
//     }

//     private void Update()
//     {
//         if (debug && Input.GetKeyDown(debugClearFrozenPreviewKey))
//             DebugClearFrozenPreview();

//         bool isSpark = core.IsSparkActive;
//         bool isDash = core.IsDashing;

//         // Detecta inicio de dash para freeze preview
//         if (debugFreezePreview && debugFreezeOnDashStart)
//         {
//             if (!wasDashing && isDash)
//             {
//                 // congela la última preview visible (si hay)
//                 float p = core.CurrentProgress01;
//                 Color c = core.ComputePhaseColor(p);
//                 DebugFreezeCurrentPreview(c);
//             }
//         }
//         wasDashing = isDash;

//         if (!isSpark)
//         {
//             SetPreviewVisible(false);
//             SetRingVisible(false);
//             SetPickupCircleVisible(false);
//             return;
//         }

//         SetPreviewVisible(true);
//         SetRingVisible(true);

//         UpdatePreviewWithBounces();
//         UpdateRing();
//     }

//     // =========================
//     // PREVIEW
//     // =========================
//     private void ConfigurePreviewAuto()
//     {
//         if (sparkPreviewLine == null)
//         {
//             var go = new GameObject("SparkPreview");
//             go.transform.SetParent(transform);
//             go.transform.localPosition = Vector3.zero;
//             sparkPreviewLine = go.AddComponent<LineRenderer>();
//         }

//         sparkPreviewLine.useWorldSpace = true;
//         sparkPreviewLine.widthCurve = AnimationCurve.Constant(0f, 1f, previewWidth);

//         Shader sh =
//             Shader.Find("Universal RenderPipeline/2D/Unlit") ??
//             Shader.Find("Universal Render Pipeline/2D/Unlit") ??
//             Shader.Find("Unlit/Color") ??
//             Shader.Find("Sprites/Default");

//         sparkPreviewLine.material = new Material(sh);
//         sparkPreviewLine.sortingLayerName = previewSortingLayer;
//         sparkPreviewLine.sortingOrder = previewSortingOrder;

//         sparkPreviewLine.numCapVertices = 4;
//         sparkPreviewLine.numCornerVertices = 2;
//         sparkPreviewLine.positionCount = 0;
//         sparkPreviewLine.enabled = false;
//     }

//     private void SetPreviewVisible(bool on)
//     {
//         if (sparkPreviewLine == null) return;

//         sparkPreviewLine.enabled = on;
//         if (!on) sparkPreviewLine.positionCount = 0;

//         SetPickupCircleVisible(on && previewMarkNextPickup);
//         if (!on) SetPickupCircleVisible(false);
//     }

//     private void UpdatePreviewWithBounces()
//     {
//         float progress = core.CurrentProgress01;
//         float mult = core.ComputeMultiplier(progress);
//         Color c = core.ComputePhaseColor(progress);

//         Vector2 dir = core.GetAimDirection8();
//         if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
//         dir.Normalize();

//         float scale = Mathf.Clamp(core.dashDistanceScale, 0.5f, 1f);
//         float totalDist = (core.dashSpeed * mult) * Mathf.Max(0.01f, core.dashDuration) * scale;

//         Vector2 pos = core.RbPosition;
//         float remaining = totalDist;
//         int bounces = 0;

//         pts.Clear();
//         pts.Add(pos);

//         SetPickupCircleVisible(false);

//         int safety = 0;
//         const int SAFETY_MAX = 64;

//         while (remaining > 0f && bounces <= previewMaxBounces && pts.Count < (previewSegments + 2) && safety++ < SAFETY_MAX)
//         {
//             // pickup marker
//             if (previewMarkNextPickup)
//             {
//                 FlameSparkPickup pickup;
//                 float pickupDist;
//                 float castDist = remaining + core.dashSkin + core.pickupSweepExtra;
//                 if (core.TryFindPickupAlong(pos, dir, castDist, out pickup, out pickupDist))
//                 {
//                     if (pickup != null && pickupDist <= remaining + core.dashSkin)
//                     {
//                         Vector2 anchor = pickup.GetAnchorWorld();
//                         pts.Add(anchor);

//                         SetPickupCircleVisible(true);
//                         DrawPickupCircle(anchor, pickupCircleRadius, c);

//                         remaining = 0f;
//                         break;
//                     }
//                 }
//             }

//             RaycastHit2D hit;
//             if (!core.CastWallsFrom(pos, dir, remaining + core.dashSkin, out hit))
//             {
//                 pos += dir * remaining;
//                 pts.Add(pos);
//                 remaining = 0f;
//                 break;
//             }

//             float travel = Mathf.Max(0f, hit.distance - core.dashSkin);

//             // MISMO FIX: no abortar por hit inmediato
//             if (travel <= 0.0001f)
//             {
//                 if (!core.dashBouncesOnWalls || bounces >= previewMaxBounces)
//                 {
//                     remaining = 0f;
//                     break;
//                 }

//                 dir = Vector2.Reflect(dir, hit.normal).normalized;
//                 bounces++;

//                 float microPush = Mathf.Max(0.0025f, core.dashSkin);
//                 pos += dir * microPush;
//                 pts.Add(pos);

//                 continue;
//             }

//             pos += dir * travel;
//             pts.Add(pos);

//             remaining -= travel;
//             if (remaining <= 0f) break;

//             if (!core.dashBouncesOnWalls) break;
//             if (bounces >= previewMaxBounces) break;

//             dir = Vector2.Reflect(dir, hit.normal).normalized;
//             bounces++;

//             float nudge = Mathf.Max(0.0025f, core.dashSkin);
//             pos += dir * nudge;
//             pts.Add(pos);
//         }

//         sparkPreviewLine.positionCount = pts.Count;
//         for (int i = 0; i < pts.Count; i++)
//             sparkPreviewLine.SetPosition(i, pts[i]);

//         sparkPreviewLine.startColor = c;
//         sparkPreviewLine.endColor = c;
//         if (sparkPreviewLine.material != null) sparkPreviewLine.material.color = c;
//     }

//     // =========================
//     // RING
//     // =========================
//     private void ConfigureRingAuto()
//     {
//         if (sparkRingLine == null)
//         {
//             var go = new GameObject("SparkRing");
//             go.transform.SetParent(transform);
//             go.transform.localPosition = Vector3.zero;
//             sparkRingLine = go.AddComponent<LineRenderer>();
//         }

//         sparkRingLine.useWorldSpace = true;
//         sparkRingLine.widthCurve = AnimationCurve.Constant(0f, 1f, ringWidth);

//         Shader sh =
//             Shader.Find("Universal RenderPipeline/2D/Unlit") ??
//             Shader.Find("Universal Render Pipeline/2D/Unlit") ??
//             Shader.Find("Unlit/Color") ??
//             Shader.Find("Sprites/Default");

//         sparkRingLine.material = new Material(sh);
//         sparkRingLine.sortingLayerName = ringSortingLayer;
//         sparkRingLine.sortingOrder = ringSortingOrder;

//         sparkRingLine.numCapVertices = 4;
//         sparkRingLine.numCornerVertices = 2;
//         sparkRingLine.positionCount = 0;
//         sparkRingLine.enabled = false;
//     }

//     private void SetRingVisible(bool on)
//     {
//         if (sparkRingLine == null) return;
//         sparkRingLine.enabled = on;
//         if (!on) sparkRingLine.positionCount = 0;
//     }

//     private void UpdateRing()
//     {
//         float total = core.SparkTimerTotal;
//         if (total <= 0f) return;

//         float remaining01 = Mathf.Clamp01(core.SparkTimer / total);
//         Color c = core.ComputePhaseColor(core.CurrentProgress01);

//         if (remaining01 <= 0f)
//         {
//             sparkRingLine.positionCount = 0;
//             return;
//         }

//         int segs = Mathf.Max(8, Mathf.RoundToInt(ringSegments * remaining01));
//         segs = Mathf.Clamp(segs, 8, ringSegments);

//         Vector2 center = core.RbPosition;

//         float a0 = 0f;
//         float a1 = Mathf.PI * 2f * remaining01;

//         sparkRingLine.positionCount = segs + 1;

//         for (int i = 0; i <= segs; i++)
//         {
//             float t = i / (float)segs;
//             float a = Mathf.Lerp(a0, a1, t);
//             Vector3 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringRadius;
//             sparkRingLine.SetPosition(i, p);
//         }

//         sparkRingLine.startColor = c;
//         sparkRingLine.endColor = c;
//         if (sparkRingLine.material != null) sparkRingLine.material.color = c;
//     }

//     // =========================
//     // Pickup Circle
//     // =========================
//     private void ConfigurePickupCircleAuto()
//     {
//         if (pickupPreviewCircle == null)
//         {
//             var go = new GameObject("PickupCircle");
//             go.transform.SetParent(transform);
//             go.transform.localPosition = Vector3.zero;
//             pickupPreviewCircle = go.AddComponent<LineRenderer>();
//         }

//         pickupPreviewCircle.useWorldSpace = true;
//         pickupPreviewCircle.widthCurve = AnimationCurve.Constant(0f, 1f, pickupCircleWidth);

//         Shader sh =
//             Shader.Find("Universal RenderPipeline/2D/Unlit") ??
//             Shader.Find("Universal Render Pipeline/2D/Unlit") ??
//             Shader.Find("Unlit/Color") ??
//             Shader.Find("Sprites/Default");

//         pickupPreviewCircle.material = new Material(sh);
//         pickupPreviewCircle.sortingLayerName = pickupCircleSortingLayer;
//         pickupPreviewCircle.sortingOrder = pickupCircleSortingOrder;

//         pickupPreviewCircle.numCapVertices = 4;
//         pickupPreviewCircle.numCornerVertices = 2;

//         pickupPreviewCircle.positionCount = 0;
//         pickupPreviewCircle.loop = true;
//         pickupPreviewCircle.enabled = false;
//     }

//     private void SetPickupCircleVisible(bool on)
//     {
//         if (pickupPreviewCircle == null) return;
//         pickupPreviewCircle.enabled = on;
//         if (!on) pickupPreviewCircle.positionCount = 0;
//     }

//     private void DrawPickupCircle(Vector2 center, float radius, Color c)
//     {
//         if (pickupPreviewCircle == null) return;

//         int segs = Mathf.Max(8, pickupCircleSegments);
//         pickupPreviewCircle.positionCount = segs;

//         for (int i = 0; i < segs; i++)
//         {
//             float a = (i / (float)segs) * Mathf.PI * 2f;
//             Vector3 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
//             pickupPreviewCircle.SetPosition(i, p);
//         }

//         pickupPreviewCircle.startColor = c;
//         pickupPreviewCircle.endColor = c;
//         if (pickupPreviewCircle.material != null) pickupPreviewCircle.material.color = c;
//     }

//     // =========================
//     // DEBUG - Frozen Preview (permanent)
//     // =========================
//     private void EnsureFrozenLine()
//     {
//         if (frozenPreviewLine != null) return;

//         var go = new GameObject("FrozenSparkPreview");
//         go.transform.SetParent(null);

//         frozenPreviewLine = go.AddComponent<LineRenderer>();
//         frozenPreviewLine.useWorldSpace = true;
//         frozenPreviewLine.widthCurve = AnimationCurve.Constant(0f, 1f, frozenLineWidth);

//         Shader sh =
//             Shader.Find("Universal RenderPipeline/2D/Unlit") ??
//             Shader.Find("Universal Render Pipeline/2D/Unlit") ??
//             Shader.Find("Unlit/Color") ??
//             Shader.Find("Sprites/Default");

//         frozenPreviewLine.material = new Material(sh);
//         frozenPreviewLine.sortingLayerName = previewSortingLayer;
//         frozenPreviewLine.sortingOrder = frozenLineSortingOrder;

//         frozenPreviewLine.numCapVertices = 4;
//         frozenPreviewLine.numCornerVertices = 2;
//         frozenPreviewLine.positionCount = 0;
//         frozenPreviewLine.enabled = false;
//     }

//     private void DebugFreezeCurrentPreview(Color c)
//     {
//         if (sparkPreviewLine == null) return;
//         if (sparkPreviewLine.positionCount < 2) return;

//         EnsureFrozenLine();

//         int n = sparkPreviewLine.positionCount;
//         frozenPreviewLine.positionCount = n;

//         for (int i = 0; i < n; i++)
//             frozenPreviewLine.SetPosition(i, sparkPreviewLine.GetPosition(i));

//         frozenPreviewLine.startColor = c;
//         frozenPreviewLine.endColor = c;
//         if (frozenPreviewLine.material != null) frozenPreviewLine.material.color = c;

//         frozenPreviewLine.enabled = true;
//         frozenHasData = true;
//     }

//     private void DebugClearFrozenPreview()
//     {
//         if (frozenPreviewLine == null) return;
//         frozenPreviewLine.positionCount = 0;
//         frozenPreviewLine.enabled = false;
//         frozenHasData = false;
//     }
// }
