---
last_updated: 2026-04-30
audience: human+ai
---

# Photon AppID Local Override Runbook

> 문서 상태: accepted_candidate (2026-04-27 박병준 작성, S14P31A303-153 산출물)
> 근거: `01_Jira_Git_컨벤션.md` §16 (Photon AppID / Secret 운영). §16.5 인벤토리 항목 5건을 본 문서로 풀어 정의한다.
> binding 범위: Photon Fusion 2의 AppID·라이선스·Secret을 로컬에 주입하는 전체 절차. 실제 GitLab Protected Branch / MR 정책이 우선한다.
> 실행 중단 조건: tracked 파일에 실제 AppID가 들어간 채로 push 시도, Mattermost 외 채널로 AppID 공유 시도, `.gitignore` 패턴 미일치 상태에서 local override 생성.

## 0. 한 줄 요약

`unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` 의 `AppIdFusion` 등은 영구 빈 placeholder로 둔다. 실제 AppID는 박병준이 발급·보관하며 Mattermost DM으로 1:1 전달한다. 로컬 주입은 **Editor Play 직전 placeholder를 임시 덮어쓰기**(commit 금지) 또는 **`PhotonAppSettings.local.asset` 사이드카(`.gitignore` 차단)** 두 가지 경로 중 택1로 진행한다.

> **외부 배포 빌드 정책**: 발표/데모 빌드는 반드시 박병준 PC(sidecar 보유자 PC)에서만 실행한다. 빌드 산출물(`Build/`, `Builds/`)을 외부에 배포하거나 sidecar 미보유 PC에서 빌드하면 placeholder 바이너리가 출하된다. 빌드 훅(§3.4)이 자동 주입·복원하지만, 훅 동작 전 sidecar 보유 여부를 반드시 확인한다.

---

## 1. Placeholder 검증

### 1.1 정본 위치

- 파일: `unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`
- meta guid 고정 (Resources/ load 키), 절대 rename·이동 금지.
- PUN(레거시) `PhotonServerSettings.asset` 은 본 프로젝트에서 사용하지 않음 (Fusion 2 only).

### 1.2 placeholder 상태 (정본 — 모든 push에서 이 모양)

```yaml
AppSettings:
  AppIdFusion:
  AppIdChat:
  AppIdVoice:
  AppVersion:
  UseNameServer: 1
  FixedRegion:
  Server:
  Port: 0
  ProxyServer:
  Protocol: 0
  EnableProtocolFallback: 1
  AuthMode: 0
  EnableLobbyStatistics: 0
  encryptionMode: 0
  emptyRoomTtl: 0
```

핵심: `AppIdFusion`/`AppIdChat`/`AppIdVoice`/`AppVersion`/`FixedRegion`/`Server` **여섯 필드는 항상 빈 값**. tracked diff에 실제 값이 들어가면 차단한다.

### 1.3 검증 명령

```bash
# (1) tracked 상태에서 6개 필드가 빈 값인지 확인 (값이 있으면 grep이 출력 → fail)
git show develop:unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset \
  | grep -E "^  (AppIdFusion|AppIdChat|AppIdVoice|AppVersion|FixedRegion|Server):" \
  | grep -vE ":\s*$" || true

# 정상: 0줄 출력 + exit code 1 (grep 매칭 없음 = leak 없음).
# 비정상: 1줄 이상 출력 + exit code 0 → secret leak. 즉시 중단하고 §4.2 사후 조치.
# 주의: `set -e` 활성 환경(CI / strict bash) 에서는 마지막 `|| true` 필수 — 매칭 0건이 false positive 로 잡히는 것 차단.
```

---

## 2. Local Override 파일명·배치·gitignore

### 2.1 패턴 (`.gitignore` 78~86 line)

```
# Photon AppID 등 비밀값은 반드시 ignore
*.env
.env.local
.env.*.local
secrets.json
**/PhotonAppSettings.local.*
**/*.local.asset
**/*.local.asset.meta
```

### 2.2 채택 경로 (택1)

| 경로 | 파일명 | 배치 위치 | 차단 패턴 |
|---|---|---|---|
| A. 사이드카 (권장) | `PhotonAppSettings.local.asset` | `unity/Assets/Photon/Fusion/Resources/` 와 동일 폴더 | `**/PhotonAppSettings.local.*` |
| B. 환경 변수 | `.env.local` | repo 루트 또는 `unity/` 루트 | `.env.local` |

> A를 권장하는 이유: Fusion 2의 `Resources.Load<PhotonAppSettings>("PhotonAppSettings")` 로직은 sidecar를 직접 인식하지 못하지만, **Editor 스크립트(§3.2)** 가 sidecar를 읽어 정본 placeholder에 주입한다. B는 Unity Editor 외부 도구(빌드 스크립트)가 같이 읽을 때 유용.

### 2.3 사이드카 라벨 정리 (필수, A 경로 한정)

Fusion SDK는 `FusionDefaultGlobal` Unity asset label로 default `PhotonAppSettings`를 식별한다 (`Fusion.Editor.FusionGlobalScriptableObjectUtils.FindDefaultAssetPath`). **두 asset에 동시에 라벨이 붙으면** SDK가 throw하여 Editor 진입 자체가 실패한다:

```
System.InvalidOperationException: There are multiple assets of type 'PhotonAppSettings'
marked as default: '.../PhotonAppSettings.asset', '.../PhotonAppSettings.local.asset'.
Remove all labels but one.
```

**원인**: Unity Editor의 `Edit > Duplicate` (Ctrl+D) 또는 파일 복사로 sidecar를 생성하면 `.meta`의 `labels:` 블록이 함께 복제된다. Fusion SDK 재import / Resources 폴더 reimport 시점에도 누락됐던 라벨이 재기입될 수 있다.

**규칙**:
- 정본 `PhotonAppSettings.asset.meta` → `labels: [FusionDefaultGlobal]` 유지 (절대 제거 금지)
- sidecar `PhotonAppSettings.local.asset.meta` → `labels` 필드 자체가 없어야 함

**셋업 직후 1회 검증 + 정리**:

```bash
# (1) sidecar .meta 에 라벨이 들어가 있는지 확인
grep -A1 "^labels:" unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset.meta || echo "OK: no labels"

# (2) 라벨이 출력되면 .meta 에서 다음 3줄 블록을 통째로 삭제 (수동 또는 sed)
#     labels:
#     - FusionDefaultGlobal
# (Unity Editor 에서: Project 패널에서 PhotonAppSettings.local.asset 선택 →
#  Inspector 우하단 라벨 영역의 'FusionDefaultGlobal' 칩 클릭 → 'Remove' 도 가능)

# (3) 재검증
grep -A1 "^labels:" unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset.meta || echo "OK"
# 정상: "OK: no labels" 또는 "OK"
```

> sidecar `.meta` 는 `.gitignore` (§2.1 라인 86)에 의해 차단되므로 본 정리는 **로컬 한정 작업**이며 commit/push 영향 없음. 단, 라벨이 남은 채로 Editor를 띄우면 §3.2 Loader 가 첫 줄(`InjectSidecarToMemory`)부터 throw → AppID 주입 실패 → Photon 접속 불가.

### 2.4 신규 작업자 셋업 후 자동 검증

```bash
git check-ignore -v unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset
# 출력 예: .gitignore:85:**/*.local.asset  unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset

# .meta 도 함께 차단되는지 검증 (Unity 가 자동 생성한 .meta 의 leak 차단)
git check-ignore -v unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset.meta
# 출력 예: .gitignore:86:**/*.local.asset.meta  unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.local.asset.meta
# 출력이 없으면 패턴 매칭 실패 → §16.5/§16.1 위반 위험. 진행 금지.
```

---

## 3. Editor 주입 절차

### 3.1 수기 주입 (1회성, 권장 안 함)

1. Unity Editor에서 `Project` 패널 → `Assets/Photon/Fusion/Resources/PhotonAppSettings`
2. Inspector에서 `App Id Fusion`/`App Id Chat`/`App Id Voice`/`App Version` 입력
3. **저장 직전 반드시** `Ctrl+Z` 로 되돌리거나 `git checkout -- unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` 으로 placeholder 복구
4. `git status` 로 PhotonAppSettings.asset 이 unmodified 인지 확인 후에만 종료

> 위험: 3번을 잊고 commit 하면 secret leak. **권장 안 함**. 필요 시 §3.2 Editor 스크립트로 자동화.

### 3.2 Sidecar 자동 주입 (권장)

`unity/Assets/_Project/Scripts/Editor/PhotonAppSettingsLoader.cs` 구현 완료 (§3.4 빌드 훅 포함):

```csharp
// 의도: InitializeOnLoad 시점에 sidecar(PhotonAppSettings.local.asset)를
//       in-memory PhotonAppSettings 정본에 덮어씀. 디스크 placeholder는 변경 안 함.
// Fusion 2 공식 패턴 — 참고: Assets/Photon/Fusion/Editor/FusionHubWindow.cs:163,256
// API 안정성: PhotonAppSettings.TryGetGlobal / PhotonAppSettings.Global 둘 다
//   Fusion.Realtime SDK XML doc-comment(`Fusion.Realtime.xml` 라인 4911/4914) 등록된 public API.
//   외부 docs.photonengine.com 에는 미게재되나 SDK 내부에서 일관 사용 중. 권장은 TryGetGlobal (graceful fail);
//   Global 속성은 정본 미발견 시 throw 가능 → InitializeOnLoad 시점에는 부적합.
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public class PhotonAppSettingsLoader : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    // 전체 클래스 구현은 실파일 참조:
    // unity/Assets/_Project/Scripts/Editor/PhotonAppSettingsLoader.cs
    // - static constructor: Editor 진입 시 sidecar → 메모리 주입 (디스크 변경 X)
    // - OnPreprocessBuild: 빌드 직전 sidecar → 정본 디스크 임시 write + SetDirty + SaveAssets
    // - OnPostprocessBuild: 빌드 종료 후 placeholder 6필드 빈 값 복원 + SaveAssets
}
```

> 주의: `EditorUtility.SetDirty` / `AssetDatabase.SaveAssets` 는 빌드 훅(OnPreprocessBuild/OnPostprocessBuild)에서만 호출한다. Editor 진입 시(InitializeOnLoad) 호출하면 placeholder가 sidecar 값으로 디스크에 기록되어 commit 위험.

> **장애 사례 (2026-04-30)**: sidecar `.meta` 에 `FusionDefaultGlobal` 라벨이 함께 붙어 있으면 본 Loader 가 `InjectSidecarToMemory` 호출 시점에 `FindDefaultAssetPath` 에서 throw → Editor 진입 실패. §2.3 의 라벨 정리 절차로 sidecar `.meta` 의 `labels` 블록을 제거하면 즉시 복구. 신규 작업자 셋업 SOP 에 §2.3 단계 추가 권장.

### 3.3 발급·전달 (§16.4 재인용)

- 발급 담당: 박병준
- 부DRI (박병준 부재 시): 유민재 (1순위) / 최진우 (2순위)
  - 박병준 군 입대·병가·5/20 데모 당일 부재 대비. AppID 회전(§4.5) 트리거 권한 보유.
  - 자격 보관: 박병준 PC 1Password vault → 부DRI 와 sealed envelope 또는 1Password 공유 vault 동기화 (W2 착수 전 결정).
- 전달 채널: Mattermost DM (1:1) — 공개 채널·README·MR·메모 금지
- 신규 합류자: 박병준이 Mattermost DM 으로 sidecar `.asset` 파일 또는 AppID 문자열 전달
- 회전 절차는 **§4.5** 참조.

### 3.4 빌드 시 sidecar 주입 (C2 — 외부 배포 빌드 안전망)

`[InitializeOnLoad]` 만으로는 **Editor 메모리 한정** 덮어쓰기다. `Build & Build Settings` 또는 CI 빌드 산출물은 정본 placeholder 그대로 → **빌드 .exe 의 Photon 접속 실패**.

§3.2 의 `PhotonAppSettingsLoader` 는 추가로 다음 두 인터페이스를 구현한다:

| 인터페이스 | 시점 | 동작 |
|---|---|---|
| `IPreprocessBuildWithReport.OnPreprocessBuild` | 빌드 직전 | sidecar 값을 정본 `PhotonAppSettings.asset` 에 임시 write + `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets` 호출 → 빌드 산출물에 실제 AppID 포함 |
| `IPostprocessBuildWithReport.OnPostprocessBuild` | 빌드 종료 후 | 정본을 placeholder 6필드 빈 값으로 즉시 복원 + `AssetDatabase.SaveAssets` |

**보안 가드**:
- 빌드 산출물(`Build/`, `Builds/`) 자체는 **외부 배포 금지** 정책 (§0 박스 참조). 데모는 박병준 PC 또는 sidecar 보유자 PC 에서만 실행.
- 빌드 실패/중단 시에도 placeholder 복원 — `OnPostprocessBuild` 는 빌드 성공/실패 무관 호출됨. 중단 안 됨 시 수동 복원: `git checkout -- unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- AppID 가 디스크에 잠시라도 기록되므로 **빌드 직후 git status 확인 필수** — `PhotonAppSettings.asset` 이 modified 면 §1.3 검증 명령 재실행.

---

## 4. CI / MR 검증

### 4.1 현재 상태

- `.gitlab-ci.yml` 이 현재 레포 루트에 존재하며, Unity build job(`build:server-linux`, `build:client-windows`)에서 `PHOTON_APP_ID_FUSION`, `PHOTON_APP_VERSION` 같은 GitLab variable을 읽어 `PhotonAppSettings.local.asset` sidecar를 런타임 생성 후 빌드 종료 시 삭제한다.
- 자동 파이프라인에서는 `changes` 규칙으로 관련 경로가 바뀐 Unity build job만 생성되고, `web` 수동 실행은 `TARGET` 값만 맞으면 `changes`와 무관하게 build/deploy job을 강제 실행할 수 있다.
- 현재 파이프라인에는 AppID 평문 유출을 막는 전용 secret-scan job은 없다. 따라서 **MR 작성자 self-check + 리뷰어 grep 검증** 을 계속 기본선으로 둔다.
- 원격 runner에서 LFS pointer checkout으로 빌드가 깨질 수 있으므로, Unity build job은 `git lfs pull` 을 선행한다.

### 4.2 MR self-check (작성자 책임)

```bash
# tracked diff 에 AppID 후보 문자열이 포함됐는지 검사 (경험적 패턴)
git diff develop...HEAD -- unity/ \
  | grep -E "^\+.*(AppIdFusion|AppIdChat|AppIdVoice):\s*[A-Za-z0-9]" \
  | grep -v "^+++ "

# 출력이 있으면 secret leak. push 금지.
# git restore <file> 또는 history rewrite 후 secret rotation §4.5
```

### 4.3 리뷰어 grep (수동)

리뷰어는 MR 변경 파일 중 `unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` 이 포함된 경우 다음 확인:

1. `AppIdFusion: <실제값>` 처럼 빈 placeholder 가 아닌 값이 있는지
2. 파일명이 `*.local.asset` 패턴이 아닌데 `*.local.` 로 끝나는지
3. README·문서·MR description 에 평문 AppID 가 노출됐는지

발견 시 즉시 reject + Mattermost DM 으로 작성자 호출.

### 4.4 CI 보강 후보 (전용 secret-scan job 도입 시)

`.gitlab-ci.yml` 의 lint job 에 다음 추가:

```yaml
photon-secret-scan:
  stage: lint
  image: alpine/git:latest
  rules:
    - if: '$CI_PIPELINE_SOURCE == "merge_request_event"'
  script:
    - |
      LEAK=$(git diff $CI_MERGE_REQUEST_DIFF_BASE_SHA...HEAD -- unity/ \
        | grep -E "^\+.*(AppIdFusion|AppIdChat|AppIdVoice):\s*[A-Za-z0-9]" || true)
      if [ -n "$LEAK" ]; then
        echo "SECRET LEAK DETECTED:"; echo "$LEAK"; exit 1
      fi
```

전용 secret-scan job 도입 후 본 §4.4 를 §16.5 의 "검증 방법" 정본으로 승격하고, §4.2/§4.3 은 fallback 으로 강등한다.

### 4.5 긴급 AppID 회전 절차 (유출 확인 즉시)

**RTO 목표: 60분** · **DRI: 박병준** · **부DRI: 유민재 (1순위) / 최진우 (2순위)**

#### 4.5.1 발견 트리거
- 정기: §4.2 MR self-check / §4.3 리뷰어 grep 출력 1줄 이상
- 비정기: 외부 제보, GitLab Secret Scanner 알림 (CI 도입 후), Photon Dashboard 비정상 트래픽

#### 4.5.2 단계별 SOP

| 단계 | 시점 | 담당 | 행동 |
|---|---|---|---|
| **T+0** | 즉시 | 발견자 | Mattermost `A303 공지/공유방` 에 1줄 알림 — "AppID leak 발견: <증거 링크>". DRI/부DRI 멘션 필수. |
| **T+5** | 5분 이내 | DRI (박병준) 또는 부DRI | Photon Dashboard (https://dashboard.photonengine.com) → 해당 App → "Manage" → AppID 재발급 또는 기존 AppID 비활성화 |
| **T+5** | 동시 | DRI/부DRI | Mattermost `A303 공지/공유방` 공지: "AppID 유출 확인. 기존 sidecar 삭제 요망. DM 으로 신규 AppID 전달 예정" |
| **T+10** | 10분 이내 | 전원 | 로컬 `PhotonAppSettings.local.asset` + `.env.local` 삭제. 삭제 확인 → `A303 공지/공유방` 에 thumbs-up reaction |
| **T+10~30** | git history 오염 시 | DRI/부DRI + PM | 아래 §4.5.3 git history rewrite 절차 |
| **T+30~45** | sidecar 재배포 | DRI/부DRI | 신규 AppID 를 Mattermost DM 으로 5명 전원 1:1 전달 (sidecar `.asset` 파일 또는 AppID 문자열) |
| **T+45~60** | 검증 | 전원 | sidecar 반영 + Editor Play 1회 확인 → `A303 공지/공유방` 에 완료 reaction |
| **T+60** | 회복 확인 | DRI/부DRI | §1.3 검증 명령으로 전 브랜치 재점검 + `06_의사결정_로그.md` 에 incident 로그 기재 |

#### 4.5.3 git history rewrite (T+10~30 단계, 필요 시)

**전제**: develop 브랜치 보호 규칙 일시 해제 — DRI + PM 승인 필수. 반드시 dry-run 우선.

```bash
# 1) dry-run (실제 변경 없이 영향 범위 확인)
git filter-repo \
  --path unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset \
  --invert-paths \
  --dry-run

# 2) 본 실행 (--force 필수: filter-repo 는 fresh clone 외 거부함)
git filter-repo \
  --path unity/Assets/Photon/Fusion/Resources/PhotonAppSettings.asset \
  --invert-paths \
  --force

# 3) force push (보호 규칙 해제 상태에서)
git push origin develop --force

# 4) 전 팀원 force pull (혹은 fresh clone)
# 각자: git fetch && git reset --hard origin/develop
```

#### 4.5.4 박병준 부재 시 분기

```
발견자
  └─→ DRI(박병준) 호출 (Mattermost @멘션 + 전화)
        ├─ DRI 1시간 내 응답 가능
        │     └─→ DRI 가 §4.5.2 진행
        └─ DRI 1시간 내 응답 불가 (군 입대·병가·5/20 데모 당일 부재 등)
              └─→ 부DRI 1순위 유민재 호출
                    ├─ 부DRI 응답 가능
                    │     └─→ 부DRI 가 §4.5.2 진행 (Photon Dashboard 자격 보유 전제)
                    └─ 부DRI 응답 불가
                          └─→ 부DRI 2순위 최진우 호출 → 동일 흐름
                                └─ 모두 부재 시: PM 권한으로 develop 보호 즉시 강화
                                       (push 차단) + 외부 노출 채널 모니터링
```

#### 4.5.5 사후 보고 (T+24h 이내)

- `06_의사결정_로그.md` 에 incident 항목 추가 (발견 시각, 유출 범위, 회전 결과, 재발 방지 액션)
- DRI/부DRI 가 W2 회고에 30분 단독 의제로 보고

---

## 5. AppID 미보유 시 Fallback (`70/05` 연계)

### 5.1 의도

박병준 부재·Mattermost DM 지연·Photon Cloud 장애 등으로 AppID 가 없을 때, **로컬 2P 리허설(`70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md`) 시나리오의 사전 단계**를 막지 않도록 LAN/Editor Multi 모드로 우회한다.

### 5.2 절차 — Editor MultiPeer (싱글 머신, 권장)

1. Unity Editor 의 Fusion `Sample > Toolbar` → `MultiPeer Mode = Multiple` 설정
2. Editor Play Mode 진입. 1번 client 는 Host, 2번 client 는 Client 로 자동 분기
3. AppID 없이 Editor 내부 loopback 으로 동작 — `70/05 §최소 시나리오` 1번(접속) 만 Pass 처리, 2~5번(원격 동기화 검증)은 SKIP 처리하고 사유 기록

### 5.3 절차 — LAN Host Mode (2 머신, 같은 망)

> Fusion 2 Cloud 모드에서는 LAN-only 가 공식 지원되지 않는다. 진행 전 네트워크 DRI(유민재) 협의 필수.

1. 2 PC 모두 같은 LAN. 한 쪽 PC 는 Photon Cloud 미사용 모드로 빌드 (Fusion `Server`/`Auto Host or Client` 분기)
2. NetworkRunner 의 `GameMode.Host` / `GameMode.Client` 로 명시 분기, AppId Fusion 은 의도적으로 빈 값
3. `70/05 §리허설 전제` 의 Host Mode 조건 만 부분 충족 — 70/05 리포트 양식의 "AppID 미보유로 LAN Fallback" 사유를 1순위에 기재

### 5.4 fallback 사용 시 BLOCKED 처리

5.2/5.3 으로 진행한 경우 다음 항목을 자동으로 BLOCKED 표시:

- `70/05 §증빙 기준` 중 "외부망 RTT" / "Photon Cloud 리포트" — 측정 불가
- `70/06 §6질문` 중 원격 2P 검증이 필요한 질문 (네트워크 DRI 가 식별)

머지 게이트(G1 Pass)는 5.2/5.3 결과만으로 통과 불가. AppID 확보 후 정식 리허설 재실행이 G1 조건.

---

## 6. Custom Authentication & 환경 분리 (W3 도입 권장)

### 6.1 Photon 보안 모델 재정립

Photon 공식 입장: **AppID 는 클라이언트 .exe 에 번들되는 준-공개 식별자**다. 디컴파일·메모리 덤프로 추출 가능. 따라서 본 runbook 의 placeholder 정책은 **git 레포 leak 차단**이 목적이며, **클라이언트 자체 보호는 별도 메커니즘 (Custom Auth) 으로 보강**해야 한다.

- 출처: [App ID Security forum](https://forum.photonengine.com/discussion/14656/app-id-security)
- 결정: 5/20 데모는 AppID 단독 (학생 프로젝트 컨텍스트). W3 (5/20 이후) Custom Auth 도입 검토.

### 6.2 Custom Authentication (W3 후보)

- 절차: Photon Dashboard → App Settings → Authentication → Custom Server URL 등록
- Custom Server: Spring Boot (`api/auth/photon`) 또는 SSAFY 인프라 — 검증 후 token 반환
- 클라이언트: NetworkRunner 시작 직전 `AuthenticationValues` 에 token 주입
- 본 runbook 은 W3 Story 채택 시 §6 보강 또는 별도 runbook 신설

### 6.3 prod / staging 환경 분리 (M5)

학생 프로젝트라 **별도 Photon App 을 만들지 않고 `AppVersion` 분기로 방 격리** 권장 (Photon Cloud 비용 절약):

- prod: `AppVersion = "1.0.0"` (5/20 데모 빌드)
- staging: `AppVersion = "0.9.0-staging"` (개발 빌드)
- dev 개인: `AppVersion = "0.0.1-{이니셜}"` (각자 PC sidecar 에서 설정)

클라이언트 간 `AppVersion` 다르면 자동으로 별도 룸 분리됨 (Photon 공식 동작).

### 6.4 AppVersion 활용 (M6)

- sidecar `PhotonAppSettings.local.asset` 의 `AppVersion` 필드 사용
- §3.2 Loader.cs 가 AppVersion 도 포함해서 덮어씀 (현재 코드 이미 포함)
- 데모 직전 박병준이 5명 sidecar `AppVersion` 동기화 — Mattermost DM 으로 "v1.0.0 로 통일" 공지 → 각자 sidecar `.asset` 수정 후 Editor Play

---

## 7. 변경 이력

- 2026-04-27 박병준: v0.1 작성. `S14P31A303-153` (인벤토리 #6) 산출물.
- 2026-04-27 박병준: v0.2 보정. C1(§4.5 회전 SOP 신설) + C2(§3.4 빌드 훅) + C3(Loader.cs 신규) + H1(컨벤션 §16.2 경로 통일) + H2(§3.2 API 주석 보강) + H3(§6 Custom Auth 추가) + H4(.gitignore .meta 패턴) + M2/M3/M4/M5/M6/M7 일괄 보강. 부DRI: 유민재(1순위)/최진우(2순위). 채널: A303 공지/공유방.
- 2026-04-30 박병준: v0.3 hotfix 반영. §2.3(사이드카 라벨 정리) 신설 + 기존 §2.3 → §2.4 번호 이동 + §3.2 장애 사례 박스 추가. 트리거: 박병준 PC 에서 Editor 기동 시 `FindDefaultAssetPath` throw 재현 (sidecar `.meta` 에 `FusionDefaultGlobal` 라벨 중복). 로컬 sidecar `.meta` 에서 라벨 제거하여 즉시 복구.

---

## 8. 관련 문서

- `01_Jira_Git_컨벤션.md` §16 — 본 runbook 의 상위 정책
- `70_검증_체크리스트/05_Fusion_로컬_2인_리허설.md` — §5 fallback 연계
- `90_백로그/11_2026-04-21_M1-M4_Jira_생성_대기.md` #6 (Story #6 = `S14P31A303-153`)
- `05_문서관리_규칙/06_의사결정_로그.md` D53(AppID 발급/보관/전달 결정)
- `.gitlab/CODEOWNERS` 라인 32 (`/unity/Assets/Photon/`) + 라인 38 (`/unity/Assets/_Project/Scripts/Editor/`) — 본 runbook 영향 파일 보호자 매핑
- `.gitlab/merge_request_templates/` (있다면) — secret-scan 체크리스트 cross-reference [확인 필요]
