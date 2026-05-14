# Noto Sans KR — Fonts 디렉토리

## 폰트 출처
- **이름**: Noto Sans CJK Korean (Noto Sans KR)
- **저작권**: Copyright 2014-2024 Adobe / Google LLC
- **GitHub**: https://github.com/notofonts/noto-cjk
- **Google Fonts**: https://fonts.google.com/noto/specimen/Noto+Sans+KR

## 다운로드 정보
- **다운로드 시점**: 2026-04-28
- **포함 파일**:
  - `NotoSansKR-Regular.otf`
  - `NotoSansKR-Bold.otf`

## 라이선스
SIL Open Font License Version 1.1 (OFL-1.1).
전문은 동일 디렉토리의 `LICENSE.txt` 참조.

## 연관 Jira 이슈
S14P31A303-238

## 적용 방식 (현재 프로젝트)
이 디렉토리에는 다음 4개 에셋이 함께 들어있다:

| 파일 | 역할 |
|---|---|
| `NotoSansKR-Regular.otf` | TMP Source Font (Dynamic 베이스) |
| `NotoSansKR-Bold.otf`    | TMP Source Font (Bold variant) |
| `NotoSansKR-Regular SDF.asset` | TMP_FontAsset, Atlas Population Mode = Dynamic |
| `NotoSansKR-Bold SDF.asset`    | TMP_FontAsset, Atlas Population Mode = Dynamic |

두 SDF 에셋은 `Assets/TextMesh Pro/Resources/TMP Settings.asset` 의
`m_fallbackFontAssets` 체인에 등록되어 있다 (Regular → Bold 순서).
프로젝트의 **기본 폰트는 LiberationSans SDF 그대로** 두며, 한글 글리프가
필요한 경우에만 fallback 체인을 따라 Noto Sans KR이 자동 사용된다.

## 재생성 / 변경 절차
1. 폰트 SDF 에셋을 다시 만들고 싶으면 `Window → TextMeshPro → Font Asset Creator` 에서
   Source Font File로 .otf 지정, Atlas Resolution 4096×4096, Atlas Population Mode = Dynamic,
   Render Mode = SDFAA, Sampling Point Size = 90, Padding = 9 로 Generate.
2. 생성된 `.asset`을 같은 디렉토리에 저장.
3. `Project Settings → TextMeshPro → Settings` 또는 `TMP Settings.asset` 인스펙터에서
   Fallback Font Assets 리스트에 등록 (Regular → Bold 순서 유지).
