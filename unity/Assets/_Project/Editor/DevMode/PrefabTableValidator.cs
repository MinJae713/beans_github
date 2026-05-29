#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 PrefabTable 검증 스크립트 (Editor 전용).
    /// DevModeAssetPack의 NetworkPrefabRef 슬롯(playerPrefab / bulletPrefab)이
    /// Fusion PrefabTable에 올바르게 등록되어 있는지 사전 체크한다.
    ///
    /// 검증 트리거:
    ///   - DevModeAssetPack Inspector에서 값 변경 시 (CustomEditor.OnInspectorGUI 경유)
    ///   - 메뉴: Colosseum → DevMode → Validate PrefabTable
    ///
    /// 에러 메시지 포맷:
    ///   [Colosseum] PrefabTable Validator: {assetName}.{slotName} - {reason}
    ///
    /// 검증 통과 시: 콘솔 출력 없음 (스팸 방지).
    /// 검증 실패 시: Debug.LogError — AD가 Step 2 절차를 바로 확인할 수 있도록 가이드 경로 안내.
    ///
    /// 범위 (보수적 — plan A303-226 §14 #3):
    ///   - playerPrefab / bulletPrefab NetworkPrefabRef IsValid 체크만 수행.
    ///   - 5슬롯 모두 채움 여부는 강제하지 않음 (어댑터 미사용 AssetPack도 유효).
    ///   - materials / uiSprites / audioClips / cardDataList 는 검증 범위 밖.
    /// </summary>
    public static class PrefabTableValidator
    {
        private const string GuidePath =
            "docs/72_DevMode_가이드/01_PrefabTable_등록_절차.md Step 2 참조";

        /// <summary>
        /// DevModeAssetPack의 NetworkPrefabRef 슬롯을 검증한다.
        /// DevModeAssetPackEditor 또는 메뉴 항목에서 호출한다.
        /// </summary>
        /// <param name="pack">검증 대상 DevModeAssetPack 인스턴스</param>
        /// <returns>에러가 없으면 true, 하나 이상 실패하면 false</returns>
        public static bool Validate(DevModeAssetPack pack)
        {
            if (pack == null)
                return true;

            var assetName = pack.name;
            var isValid = true;

            // ── playerPrefab 검증 ──
            if (!pack.playerPrefab.IsValid)
            {
                Debug.LogError(
                    $"[Colosseum] PrefabTable Validator: {assetName}.playerPrefab" +
                    $" - NetworkPrefabRef가 유효하지 않습니다." +
                    $" Fusion PrefabTable에 프리팹이 등록되어 있는지 확인하세요." +
                    $" ({GuidePath})",
                    pack);
                isValid = false;
            }

            // [A303-171] bulletPrefab 검증은 Visual-only projectile 전환 이후 폐기.

            return isValid;
        }

        /// <summary>
        /// 메뉴 항목: Colosseum → DevMode → Validate PrefabTable
        /// 프로젝트 내 모든 DevModeAssetPack 인스턴스를 일괄 검증한다.
        /// </summary>
        [MenuItem("Colosseum/DevMode/Validate PrefabTable")]
        private static void ValidateAll()
        {
            var guids = AssetDatabase.FindAssets("t:DevModeAssetPack");
            if (guids.Length == 0)
            {
                Debug.LogWarning(
                    "[Colosseum] PrefabTable Validator: DevModeAssetPack 인스턴스를 찾을 수 없습니다." +
                    " Assets/_Project/Data/DevMode/ 에 생성하세요.");
                return;
            }

            var failCount = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var pack = AssetDatabase.LoadAssetAtPath<DevModeAssetPack>(path);
                if (!Validate(pack))
                    failCount++;
            }

            if (failCount == 0)
                Debug.Log($"[Colosseum] PrefabTable Validator: {guids.Length}개 AssetPack 검증 완료 — 에러 없음.");
            else
                Debug.LogError(
                    $"[Colosseum] PrefabTable Validator: {failCount}/{guids.Length}개 AssetPack에서 검증 실패." +
                    " 위 에러를 확인하세요.");
        }
    }

    /// <summary>
    /// DevModeAssetPack 커스텀 인스펙터.
    /// Inspector에서 값이 바뀔 때마다 PrefabTableValidator를 자동 실행한다.
    /// DevModeAssetPack.cs 를 수정하지 않고 OnValidate 역할을 수행한다.
    /// </summary>
    [CustomEditor(typeof(DevModeAssetPack))]
    public class DevModeAssetPackEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                // 값이 변경된 직후 검증 실행
                PrefabTableValidator.Validate(target as DevModeAssetPack);
            }
        }
    }
}
#endif
