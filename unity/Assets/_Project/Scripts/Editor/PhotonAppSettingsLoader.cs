// File: unity/Assets/_Project/Scripts/Editor/PhotonAppSettingsLoader.cs
// 의도: Photon Fusion 2 의 정본 PhotonAppSettings.asset (placeholder 빈 값) 에
//       sidecar PhotonAppSettings.local.asset (gitignored 실제 AppID) 를 다음 두 시점에 주입한다:
//         1. Editor 진입 시 (InitializeOnLoad)        — 메모리에만 덮어씀, 디스크 변경 X
//         2. 빌드 직전/직후 (IPreprocess/Postprocess) — 디스크 임시 write → 빌드 → 즉시 placeholder 복원
//
// 보안: AppID 는 git tracked 파일에 영구 기록되지 않는다. 빌드 산출물 (.exe) 에는 포함되나
//       runbook §3.4 정책상 외부 배포 금지 (발표/데모는 박병준 PC 또는 sidecar 보유자 PC 에서만).
//
// API 안정성: PhotonAppSettings.TryGetGlobal / .Global 둘 다 Fusion.Realtime SDK 의 public API
//   (`Fusion.Realtime.xml` 라인 4911/4914 등록). InitializeOnLoad 에서는 graceful 한 TryGetGlobal 권장
//   (정본 asset 미발견 시 throw 회피).
//
// 참고 SDK 사용처:
//   - unity/Assets/Photon/Fusion/Editor/FusionHubWindow.cs:163, 256
//   - unity/Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs:12458, 14032
//
// 운영 문서: docs/80_Jira_Git_운영_원칙/03_Photon_AppID_Runbook.md §3.2 / §3.4
// 이상 발생 시: Mattermost A303 공지/공유방 에 알림 후 runbook §4.5 절차 진행.

using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public class PhotonAppSettingsLoader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    // ==========================================
    // 상수
    // ==========================================
    private const string SidecarPath = "Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset";
    private const string GlobalAssetPath = "Assets/Photon/Fusion/Resources/PhotonAppSettings.asset";

    // 빌드 훅 호출 순서 (낮을수록 먼저 — 정본 변경은 가능한 일찍, 다른 훅 영향 최소)
    public int callbackOrder => 0;

    // ==========================================
    // 1. Editor 진입 시 메모리 주입 (InitializeOnLoad)
    // ==========================================
    static PhotonAppSettingsLoader()
    {
        InjectSidecarToMemory();
    }

    private static void InjectSidecarToMemory()
    {
        var sidecar = AssetDatabase.LoadAssetAtPath<PhotonAppSettings>(SidecarPath);
        if (sidecar == null)
        {
            // sidecar 없음 → AppID 미보유 상태. runbook §5 fallback 경로 사용.
            // 정상 동작: 박병준 신규 합류자 onboarding 전 또는 회전 직후 일시 상태.
            return;
        }

        if (!PhotonAppSettings.TryGetGlobal(out var global))
        {
            // 정본 asset 미발견 (Fusion SDK 미설치?). 본 프로젝트에선 발생 불가 가정.
            Debug.LogWarning("[PhotonAppSettingsLoader] Global PhotonAppSettings not found. Skipped.");
            return;
        }

        // 메모리 덮어쓰기만. EditorUtility.SetDirty / AssetDatabase.SaveAssets 호출 금지 (디스크 leak).
        global.AppSettings.AppIdFusion = sidecar.AppSettings.AppIdFusion;
        global.AppSettings.AppIdChat   = sidecar.AppSettings.AppIdChat;
        global.AppSettings.AppIdVoice  = sidecar.AppSettings.AppIdVoice;
        global.AppSettings.AppVersion  = sidecar.AppSettings.AppVersion;
        global.AppSettings.FixedRegion = sidecar.AppSettings.FixedRegion;
        global.AppSettings.Server      = sidecar.AppSettings.Server;
        // 주의: SetDirty/SaveAssets 호출 금지 — 디스크 placeholder 보존이 secret 차단의 핵심.
    }

    // ==========================================
    // 2. 빌드 직전 — 정본 디스크에 sidecar 값 임시 write
    // ==========================================
    public void OnPreprocessBuild(BuildReport report)
    {
        var sidecar = AssetDatabase.LoadAssetAtPath<PhotonAppSettings>(SidecarPath);
        if (sidecar == null)
        {
            // 빌드는 placeholder 그대로 진행됨 — Photon 접속 실패. runbook §3.4 경고 박스 정책 위반 가능.
            Debug.LogError(
                "[PhotonAppSettingsLoader] Sidecar missing. Build will produce placeholder binary " +
                "(Photon connection will fail at runtime). Acquire sidecar from DRI before building. " +
                "See runbook §3.3 and contact 박병준 via Mattermost A303 공지/공유방.");
            return;
        }

        if (!PhotonAppSettings.TryGetGlobal(out var global))
        {
            Debug.LogError("[PhotonAppSettingsLoader] Global PhotonAppSettings not found at build time.");
            return;
        }

        // 디스크 write — 빌드 산출물에 실제 AppID 포함되도록.
        global.AppSettings.AppIdFusion = sidecar.AppSettings.AppIdFusion;
        global.AppSettings.AppIdChat   = sidecar.AppSettings.AppIdChat;
        global.AppSettings.AppIdVoice  = sidecar.AppSettings.AppIdVoice;
        global.AppSettings.AppVersion  = sidecar.AppSettings.AppVersion;
        global.AppSettings.FixedRegion = sidecar.AppSettings.FixedRegion;
        global.AppSettings.Server      = sidecar.AppSettings.Server;
        EditorUtility.SetDirty(global);
        AssetDatabase.SaveAssets();

        Debug.Log("[PhotonAppSettingsLoader] Pre-build: injected sidecar into global asset on disk.");
    }

    // ==========================================
    // 3. 빌드 종료 후 — 정본을 placeholder 빈 값으로 즉시 복원
    // ==========================================
    public void OnPostprocessBuild(BuildReport report)
    {
        if (!PhotonAppSettings.TryGetGlobal(out var global)) return;

        // placeholder 6필드 빈 값 복원 (runbook §1.2 정본 모양)
        global.AppSettings.AppIdFusion = string.Empty;
        global.AppSettings.AppIdChat   = string.Empty;
        global.AppSettings.AppIdVoice  = string.Empty;
        global.AppSettings.AppVersion  = string.Empty;
        global.AppSettings.FixedRegion = string.Empty;
        global.AppSettings.Server      = string.Empty;
        EditorUtility.SetDirty(global);
        AssetDatabase.SaveAssets();
        InjectSidecarToMemory();

        Debug.Log("[PhotonAppSettingsLoader] Post-build: restored placeholder. " +
                  "Verify with `git status` — if PhotonAppSettings.asset modified, re-run §1.3 verification.");
    }
}
