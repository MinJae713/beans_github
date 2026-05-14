#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Colosseum.Editor.MVPArena
{
    /// <summary>
    /// MVPArena 비주얼 자산 타일 정합 자동 검증기 (Editor 전용).
    /// 신규 GameObject 추가 시 sprite tile alignment 가 깨지지 않도록 가드한다.
    /// T3 (2026-05-12) ceil-snap fix(Option B) 이후 작업의 안전망.
    ///
    /// 검증 트리거:
    ///   - Scene 저장 시 자동 (MVPArena.unity 한정)
    ///   - 메뉴: Colosseum → MVPArena → Validate Alignment
    ///
    /// 검증 대상 sprite (PPU 100 기준 native unit):
    ///   T_FLR_* (2×1) · T_WAL_* (1×2) · T_STP_* (1×0.5) · T_RFL_* (0.5×1)
    ///   P_BOX (1×1) · P_PIP (2×0.5) · T_RMP / T_RMP_LEFT (1×1)
    ///
    /// 검증 항목:
    ///   1. sr.size == box.size (BoxCollider2D 있을 때)
    ///   2. sr.size 가 sprite native 의 정수배 (양 끝 0.5 partial tile 방지)
    ///   3. world center (transform.position + box.offset) 가 0.5 grid 위
    ///   4. transform.lossyScale 절대값 == 1 (flip 허용)
    ///   5. T_FLR/T_WAL/T_STP/T_RFL: drawMode=Tiled + tileMode=Continuous
    ///      P_BOX: drawMode=Sliced
    ///   6. sharedMaterial.shader.name == "Sprites/Default" (URP 2D Lit 어두워짐 방지)
    ///
    /// 통과 시 콘솔 출력 없음 (스팸 방지). 메뉴 호출 시에만 "OK" 한 줄.
    /// 실패 시 Debug.LogWarning + GameObject ping 가능.
    /// </summary>
    public static class MVPArenaAlignmentValidator
    {
        private const string TargetSceneSuffix = "/MVPArena.unity";
        private const string LogPrefix = "[MVPArenaAlign]";
        private const float Eps = 0.001f;

        private static readonly Dictionary<string, Vector2> NativeSizeBySprite = BuildSizeMap();

        private static Dictionary<string, Vector2> BuildSizeMap()
        {
            var map = new Dictionary<string, Vector2>();
            string[] suffix9 = { "TL", "TC", "TR", "ML", "MC", "MR", "BL", "BC", "BR" };
            string[] flrSuffix = { "L", "M", "R" };
            foreach (var s in flrSuffix) map["T_FLR_" + s] = new Vector2(2f, 1f);
            foreach (var s in suffix9)
            {
                map["T_WAL_" + s] = new Vector2(1f, 2f);
                map["T_STP_" + s] = new Vector2(1f, 0.5f);
                map["T_RFL_" + s] = new Vector2(0.5f, 1f);
            }
            map["P_BOX"] = new Vector2(1f, 1f);
            map["P_PIP"] = new Vector2(2f, 0.5f);
            map["T_RMP"] = new Vector2(1f, 1f);
            map["T_RMP_LEFT"] = new Vector2(1f, 1f);
            return map;
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!scene.path.EndsWith(TargetSceneSuffix)) return;
            ValidateScene(scene, verbose: false);
        }

        [MenuItem("Colosseum/MVPArena/Validate Alignment")]
        public static void ValidateActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning($"{LogPrefix} no active scene");
                return;
            }
            ValidateScene(scene, verbose: true);
        }

        /// <summary>
        /// Hierarchy 또는 Scene 에서 선택된 GameObject 들의 position 을 0.5 grid 에 snap +
        /// BoxCollider2D.size 를 sprite native 의 정수배(ceil)로 보정 + sr.size = box.size 동기화.
        /// Unity 의 V키 / Ctrl-drag snap 이 안 먹을 때 우회용.
        /// Alt 추가로 Unity 2022 기본 'Edit/Snapping/Snap All Axes' (Ctrl+Shift+G) 충돌 회피.
        /// Shortcut: Ctrl+Shift+Alt+G
        /// </summary>
        [MenuItem("Colosseum/MVPArena/Snap Selection to 0.5 Grid %#&g")]
        public static void SnapSelectionToGrid()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} Snap: no selection (Hierarchy 또는 Scene 에서 GameObject 를 선택하세요)");
                return;
            }
            // Undo group — 모든 변경을 단일 Ctrl+Z 로 되돌릴 수 있게 묶음.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Snap Selection to 0.5 Grid");
            int snapped = 0, sized = 0;
            foreach (var go in objs)
            {
                if (go == null) continue;
                var t = go.transform;
                Vector3 p = t.position;
                var box = go.GetComponent<BoxCollider2D>();
                Vector2 wc = (Vector2)p + (box != null ? box.offset : Vector2.zero);
                float ncx = Mathf.Round(wc.x * 2f) / 2f;
                float ncy = Mathf.Round(wc.y * 2f) / 2f;
                Vector2 delta = new Vector2(ncx - wc.x, ncy - wc.y);
                if (delta.sqrMagnitude > 0.0001f)
                {
                    Undo.RecordObject(t, "Snap to 0.5 Grid");
                    t.position = new Vector3(p.x + delta.x, p.y + delta.y, p.z);
                    EditorUtility.SetDirty(t);
                    snapped++;
                }

                // collider/sprite size ceil to int-multiple of native
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null && box != null &&
                    NativeSizeBySprite.TryGetValue(sr.sprite.name, out var native))
                {
                    float newX = Mathf.Ceil(box.size.x / native.x - 0.0001f) * native.x;
                    float newY = Mathf.Ceil(box.size.y / native.y - 0.0001f) * native.y;
                    if (Mathf.Abs(newX - box.size.x) > 0.001f || Mathf.Abs(newY - box.size.y) > 0.001f)
                    {
                        Undo.RecordObject(box, "Ceil Box Size");
                        Undo.RecordObject(sr, "Ceil Sprite Size");
                        box.size = new Vector2(newX, newY);
                        sr.size = box.size;
                        EditorUtility.SetDirty(box);
                        EditorUtility.SetDirty(sr);
                        sized++;
                    }
                }
            }
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"{LogPrefix} Snap: selection={objs.Length} · position snapped={snapped} · size ceil'd={sized}");
        }

        private const string ArtBase = "Assets/_Project/Art/MVPArena/";

        /// <summary>
        /// 선택된 SR 을 9-slice / 1D-cap 자동 매핑.
        /// 단일 SR (drawMode=Tiled, sprite=_MC/_M) 을 N×M tile grid 자식들로 분할 +
        /// 각 자식에 적절한 corner / edge / center variant sprite 할당.
        /// 원본 SR 은 enabled=false (collider/메커닉 유지).
        ///
        /// 대상: T_FLR_* (1D), T_WAL_* / T_STP_* / T_RFL_* (9-slice).
        /// Shortcut: Ctrl+Shift+Alt+L (Layered caps, Alt 추가로 기본 단축키 충돌 회피)
        /// </summary>
        [MenuItem("Colosseum/MVPArena/Apply Caps to Selection %#&l")]
        public static void ApplyCapsToSelection()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} ApplyCaps: no selection");
                return;
            }
            // Undo group — child destroy + create + SR.enabled toggle 을 단일 Ctrl+Z 로 복원.
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Caps to Selection");
            int processed = 0, skipped = 0;
            foreach (var go in objs)
            {
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) { skipped++; continue; }
                string sp = sr.sprite.name;

                if (sp.StartsWith("T_FLR_"))
                {
                    if (ApplyCapsInternal(go, sr, "T_FLR", new Vector2(2f, 1f), is1D: true)) processed++;
                    else skipped++;
                }
                else if (sp.StartsWith("T_WAL_") || sp.StartsWith("T_STP_") || sp.StartsWith("T_RFL_"))
                {
                    string fam = sp.Substring(0, 5); // "T_WAL"
                    Vector2 native = fam == "T_WAL" ? new Vector2(1f, 2f)
                                  : fam == "T_STP" ? new Vector2(1f, 0.5f)
                                  : new Vector2(0.5f, 1f); // T_RFL
                    if (ApplyCapsInternal(go, sr, fam, native, is1D: false)) processed++;
                    else skipped++;
                }
                else skipped++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"{LogPrefix} ApplyCaps: processed={processed} · skipped={skipped}");
        }

        private static bool ApplyCapsInternal(GameObject go, SpriteRenderer sr, string fam, Vector2 native, bool is1D)
        {
            var box = go.GetComponent<BoxCollider2D>();
            if (box == null) return false;

            int N = Mathf.Max(1, Mathf.RoundToInt(box.size.x / native.x));
            int M = is1D ? 1 : Mathf.Max(1, Mathf.RoundToInt(box.size.y / native.y));
            if (N == 1 && M == 1) return false; // nothing to split

            // remove old child caps (re-runnable)
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                var c = go.transform.GetChild(i);
                if (c.name.StartsWith("_Cap_")) Undo.DestroyObjectImmediate(c.gameObject);
            }

            // disable original SR (keep collider)
            Undo.RecordObject(sr, "Apply Caps");
            sr.enabled = false;
            EditorUtility.SetDirty(sr);

            // origin of grid = bottom-left in local space
            float originX = -(N - 1) * native.x * 0.5f;
            float originY = -(M - 1) * native.y * 0.5f;

            for (int i = 0; i < N; i++)
            for (int j = 0; j < M; j++)
            {
                string suffix = SuffixFor(i, j, N, M, is1D);
                string spName = fam + (is1D ? "_" + suffix : "_" + suffix);
                var newSp = AssetDatabase.LoadAssetAtPath<Sprite>(ArtBase + spName + ".png");
                if (newSp == null)
                {
                    Debug.LogWarning($"{LogPrefix} missing sprite {spName}.png — skipping ({i},{j})");
                    continue;
                }

                var child = new GameObject($"_Cap_{i:D2}_{j:D2}_{suffix}");
                Undo.RegisterCreatedObjectUndo(child, "Apply Caps");
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = new Vector3(originX + i * native.x, originY + j * native.y, 0f);
                child.transform.localScale = Vector3.one;

                var csr = child.AddComponent<SpriteRenderer>();
                csr.sprite = newSp;
                csr.color = Color.white;
                csr.drawMode = SpriteDrawMode.Simple;
                csr.sortingOrder = sr.sortingOrder;
                csr.sortingLayerID = sr.sortingLayerID;
                csr.sharedMaterial = sr.sharedMaterial;
                EditorUtility.SetDirty(csr);
            }
            EditorUtility.SetDirty(go);
            return true;
        }

        // 9-slice suffix: corner(TL/TR/BL/BR) + edge(TC/BC/ML/MR) + center(MC).
        // is1D 면 _L / _M / _R 만 사용.
        private static string SuffixFor(int i, int j, int N, int M, bool is1D)
        {
            if (is1D)
            {
                if (N == 1) return "M";
                if (i == 0) return "L";
                if (i == N - 1) return "R";
                return "M";
            }
            // 9-slice (Y up: j=M-1 is top)
            string h = (N == 1) ? "C" : (i == 0 ? "L" : (i == N - 1 ? "R" : "C"));
            string v = (M == 1) ? "M" : (j == M - 1 ? "T" : (j == 0 ? "B" : "M"));
            return v + h;
        }

        /// <summary>
        /// 선택된 GameObject 들의 SpriteRenderer.color 를 white(1,1,1,a) 로 reset.
        /// placeholder 시절 dark tint 가 들러붙어 sprite 색이 어둡게 보일 때 사용.
        /// Shortcut: Ctrl+Shift+Alt+W (Alt 추가로 OS/IDE 단축키 충돌 회피)
        /// </summary>
        [MenuItem("Colosseum/MVPArena/Reset Selection Color to White %#&w")]
        public static void ResetSelectionColorWhite()
        {
            var objs = Selection.gameObjects;
            if (objs == null || objs.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} ResetColor: no selection");
                return;
            }
            int reset = 0;
            foreach (var go in objs)
            {
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                if (sr.color.r > 0.99f && sr.color.g > 0.99f && sr.color.b > 0.99f) continue;
                Undo.RecordObject(sr, "Reset SR Color to White");
                sr.color = new Color(1f, 1f, 1f, sr.color.a);
                EditorUtility.SetDirty(sr);
                reset++;
            }
            Debug.Log($"{LogPrefix} ResetColor: selection={objs.Length} · reset={reset}");
        }

        private static void ValidateScene(Scene scene, bool verbose)
        {
            var allSr = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
            int total = 0, issues = 0;

            foreach (var sr in allSr)
            {
                if (sr == null || sr.sprite == null) continue;
                var go = sr.gameObject;
                if (go == null) continue;
                if (go.scene != scene) continue;
                if (PrefabUtility.IsPartOfPrefabAsset(go)) continue;

                string spName = sr.sprite.name;
                if (!NativeSizeBySprite.TryGetValue(spName, out var native)) continue;

                total++;
                var problems = new List<string>();
                var box = sr.GetComponent<BoxCollider2D>();

                // 1. sr.size == box.size
                if (box != null)
                {
                    if (Mathf.Abs(sr.size.x - box.size.x) > Eps ||
                        Mathf.Abs(sr.size.y - box.size.y) > Eps)
                    {
                        problems.Add($"sr.size {sr.size} != box.size {box.size}");
                    }
                }

                // 2. sr.size int-multiple of native
                float ratioX = sr.size.x / native.x;
                float ratioY = sr.size.y / native.y;
                if (Mathf.Abs(ratioX - Mathf.Round(ratioX)) > Eps)
                {
                    problems.Add($"size.x={sr.size.x:F2} not int-multiple of native.x={native.x}");
                }
                if (Mathf.Abs(ratioY - Mathf.Round(ratioY)) > Eps)
                {
                    problems.Add($"size.y={sr.size.y:F2} not int-multiple of native.y={native.y}");
                }

                // 3. world center on 0.5 grid
                Vector2 wc = (Vector2)sr.transform.position + (box != null ? box.offset : Vector2.zero);
                float fx2 = wc.x * 2f - Mathf.Round(wc.x * 2f);
                float fy2 = wc.y * 2f - Mathf.Round(wc.y * 2f);
                if (Mathf.Abs(fx2) > Eps || Mathf.Abs(fy2) > Eps)
                {
                    problems.Add($"world center ({wc.x:F3},{wc.y:F3}) not on 0.5 grid");
                }

                // 4. lossyScale ±1
                var ls = sr.transform.lossyScale;
                if (Mathf.Abs(Mathf.Abs(ls.x) - 1f) > Eps ||
                    Mathf.Abs(Mathf.Abs(ls.y) - 1f) > Eps)
                {
                    problems.Add($"lossyScale {ls} != ±1");
                }

                // 5. drawMode / tileMode
                bool isTiledFamily =
                    spName.StartsWith("T_FLR_") ||
                    spName.StartsWith("T_WAL_") ||
                    spName.StartsWith("T_STP_") ||
                    spName.StartsWith("T_RFL_");
                if (isTiledFamily)
                {
                    if (sr.drawMode != SpriteDrawMode.Tiled)
                        problems.Add($"drawMode={sr.drawMode} (expected Tiled)");
                    else if (sr.tileMode != SpriteTileMode.Continuous)
                        problems.Add($"tileMode={sr.tileMode} (expected Continuous)");
                }
                else if (spName == "P_BOX")
                {
                    if (sr.drawMode != SpriteDrawMode.Sliced)
                        problems.Add($"drawMode={sr.drawMode} (expected Sliced for P_BOX)");
                }

                // 6. material shader == "Sprites/Default" (Unlit).
                // Unity 가 인스턴스화한 material 은 이름이 "Sprites-Default-Runtime" 등으로 바뀌므로
                // shader.name 으로 비교한다. URP 2D Sprite-Lit-Default 는 light 없을 때 어두워진다.
                var mat = sr.sharedMaterial;
                if (mat != null && mat.shader != null && mat.shader.name != "Sprites/Default")
                {
                    problems.Add($"shader={mat.shader.name} (expected Sprites/Default — URP 2D Lit causes dark plate)");
                }

                if (problems.Count > 0)
                {
                    issues++;
                    Debug.LogWarning(
                        $"{LogPrefix} {go.name} ({spName}): {string.Join(" · ", problems)}",
                        go);
                }
            }

            if (verbose)
            {
                if (issues == 0)
                    Debug.Log($"{LogPrefix} scene='{scene.name}' total={total} · all OK");
                else
                    Debug.Log($"{LogPrefix} scene='{scene.name}' total={total} · issues={issues}");
            }
            else if (issues > 0)
            {
                Debug.Log($"{LogPrefix} scene='{scene.name}' total={total} · issues={issues} (auto-check on save)");
            }
        }
    }
}
#endif
