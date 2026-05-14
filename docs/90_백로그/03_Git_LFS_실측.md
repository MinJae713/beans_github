---
last_updated: 2026-05-14
audience: human+ai
---

# Git LFS 실측 및 운영 정책 (S14P31A303-152)

> 상태: accepted_candidate (박병준, 2026-04-27 측정 + 정책 v0.1)
> 정본 컨벤션: [`80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션.md`](../80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션.md) §12.5
> Jira: [S14P31A303-152](https://ssafy.atlassian.net/browse/S14P31A303-152) [INFRA] Git LFS 용량 실측 및 초과 대응안 확정

## 0. 결론 (TL;DR)

- **SSAFY GitLab 강제 LFS 쿼터 없음** (self-hosted CE/EE, admin-level cap 미설정)
- 현재 LFS 사용량 표는 **2026-04-27 시점 snapshot** 이다.
- 2026-05-14 기준 운영 메모: 자체 게임 에셋(`unity/Assets/_Project/UI/Logo/Colosseum_Logo.png`, `unity/Assets/_Project/Fonts/NotoSansKR-*.otf` 등)도 LFS 대상에 포함되어 있으며, 원격 runner 빌드 전 `git lfs pull` 이 필요함이 확인되었다.
- 운영 정책: 자체 임계 (단일 50 MB / 누적 5 GB) 도달 시 압축 + 외부 스토리지 분기

## 1. 측정 메타

| 항목 | 값 |
|---|---|
| 측정 일자 | 2026-04-27 |
| 측정자 | 박병준 |
| 대상 리포 | `lab.ssafy.com/s14-final/S14P31A303` |
| 대상 리비전 | origin/develop @ `61f8248` |
| 도구 | `git lfs ls-files --size`, `du -sh`, GitLab Project Usage Quotas (web UI) |

## 2. 실측 결과

### 2.1 SSAFY GitLab 프로젝트 사용량 (web UI)

URL: `https://lab.ssafy.com/s14-final/S14P31A303/-/usage_quotas`

| Storage type | Usage | % |
|---|---|---|
| Repository (Git) | 8.26 MiB | 89.6% |
| LFS (Audio/Video/Datasets/Graphics) | **978.34 KiB** | 10.4% |
| Job artifacts | 0 B | — |
| Packages | 0 B | — |
| Snippets | 0 B | — |
| Wiki | 0 B | — |
| Container registry | 0 B | — |
| **Total** | **9.2 MiB** | — |

### 2.2 로컬 측정 (`git lfs ls-files --size` 합산)

| 항목 | 값 |
|---|---|
| LFS 추적 파일 수 | 46개 |
| LFS 총 용량 (checked-out) | 0.98 MB |
| 로컬 LFS 캐시 (`.git/lfs/objects`) | 1.1 MB |
| `.git` 전체 | 8.8 MB |

GitLab 측정값(978.34 KiB)과 로컬 측정값(0.98 MB)은 동일 — 정합성 확인 ✅.

### 2.3 LFS 추적 파일 출처 (확장자별)

| 확장자 | 개수 | 비고 |
|---|---|---|
| `.png` | 42 | 전부 `unity/Assets/Photon/Fusion/Editor/EditorResources/` 또는 `unity/Assets/TextMesh Pro/` |
| `.ttf` | 4 | LiberationSans 350 KB, Roboto 168 KB, Oswald-Header 153 KB, JetBrainsMono 112 KB |

→ 위 수치는 **2026-04-27 측정 시점** 기준이다. 현재 운영 상태에서는 `_Project` 내부 게임 에셋도 LFS 대상에 포함되므로, 이 행을 현재 상태 truth로 재사용하지 않는다.

### 2.4 작업 트리

| 디렉토리 | 크기 | 비고 |
|---|---|---|
| `unity/Library/` | 2.3 GB | gitignore (정상, tracked 아님) |
| `unity/Assets/` | 17 MB | Photon 9.7 MB + 자체 7.3 MB |
| `unity/Temp/` | 6.4 MB | gitignore |
| `unity/Logs/` | 2.2 MB | gitignore |

## 3. SSAFY GitLab 쿼터 확인

### 3.1 프로젝트 레벨

`Settings > Usage Quotas` 페이지 — 사용량 표시만 있고 cap 표시 없음.

### 3.2 그룹 레벨

`https://lab.ssafy.com/groups/s14-final/-/usage_quotas` → **404 Not Found**.

→ SSAFY 관리자가 admin-level group quota 정책을 설정하지 않은 것으로 판단. **강제 한도 없음.**

## 4. 운영 정책 (대응안)

> 강제 쿼터가 없으므로 자체 임계를 정의해 운영한다.

### 4.1 임계 1 — 단일 에셋 50 MB 초과

- LFS에 진입시키지 않고 **외부 스토리지로 분기** (SSAFY Drive / Google Drive / Notion 첨부)
- 레포에는 다운로드 링크 + SHA256 체크섬을 포함한 README 또는 placeholder 파일을 둔다
- 적용 대상 예: 영상 캡처(>50 MB), Substance Painter 작업본, 4K 텍스처 원본, raw 캡처 사운드

### 4.2 임계 2 — LFS 누적 5 GB 도달

- **사전 알림 (4 GB 초과)**: 주간 모니터링에서 감지 시 Mattermost `#dev-infra` 사전 공지
- **5 GB 도달**: SSAFY 인프라 팀에 admin-level 쿼터 정책 문의 + §4.3 압축 정책 즉시 강화 + §4.1 임계를 25 MB로 하향
- **10 GB 도달**: 외부 스토리지 전면 이전 검토 + 마스터 브랜치 LFS purge 검토

### 4.3 상시 압축 정책

| 자산 | 정책 |
|---|---|
| PSD | 작업본은 `working/` 별도 디렉토리(`.gitignore`), 커밋용은 PNG 변환 |
| 오디오 | OGG 96 kbps 기준. WAV 무압축 원본 직접 커밋 금지 |
| 스프라이트 | Sprite Atlas 적용. 개별 PNG 30개 이상 누적 시 아틀라스 합치기 |
| 텍스처 | 2K 이하 권장. 4K는 §4.1 외부 스토리지로 |

### 4.4 모니터링 루틴

- **주간 (매주 월요일)**: 박병준이 `git lfs ls-files --size` 합산 + GitLab Usage 페이지 확인 → Mattermost `#dev-infra` 공유
- **MR 단계**: 리뷰어가 신규 LFS 진입 파일이 50 MB 초과인지 확인 (MR 본문 §13.2 체크 포인트로 추가 권장)
- **Gate 시점 (G1~G4)**: 게이트 통과 직전 본 문서 §2 표 재측정 + 수치 갱신

## 5. 컨벤션 연결

본 정책은 컨벤션 §12.5 의 "quota 초과 시 대응" 항목을 구체화한 운영 가이드다.

| 위치 | 역할 |
|---|---|
| 컨벤션 §12.5 | 원칙 + 임계 수치 (요약) |
| 본 문서 | 임계 발동 절차 + 측정 루틴 + 측정 이력 |

## 6. 변경 이력

| 일자 | 변경 | 작성자 |
|---|---|---|
| 2026-05-14 | runner 빌드 장애 분석 결과 반영: `_Project` 내부 게임 에셋 LFS 사용 + `git lfs pull` 필요 운영 메모 추가 | Codex |
| 2026-04-27 | 초기 측정 + 정책 v0.1 작성 (S14P31A303-152) | 박병준 |
