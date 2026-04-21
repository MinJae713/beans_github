<!--
MR 제목 형식: [A303-{이슈번호}] [타입]: [간단한 설명]
타입: feat | fix | refactor | chore | docs | art | audio | hotfix | test
근거: docs/80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션.md §13
-->

## 기본 정보

- **source_branch**: `<!-- 예: feature/gameplay/card/A303-45-poison-card -->`
- **target_branch**: `develop`
- **Jira 이슈**: [A303-](https://ssafy.atlassian.net/browse/A303-)
- **도메인 구분자**: `[GAMEPLAY|NETWORK|PHYSICS|UI|ART|AUDIO|INFRA|PLAN|API]`

## 변경 요약

<!-- 무엇이·왜 바뀌었는지 3~5줄 -->

## 테스트 방법

<!-- Unity Editor Play / 해당 씬 확인 / Fusion 로컬 2P / 자동 테스트 여부 -->

## 씬/프리팹 변경

<!-- 있으면 파일명과 소유자 확인. 없으면 "없음".
     Player.prefab / Bullet.prefab 등 공용 프리팹 수정 시 [SCENE-LOCK] 공지 확인 (§15) -->

## NetworkObject 변경

<!-- Y/N + 내용. [Networked] 프로퍼티 추가/변경·NetworkProjectConfig 수정 시
     유민재(@dbalsend) 게이트 리뷰 필수 -->

## 관련 기준문서

<!-- 예: docs/50_기술_구조_기준/01_Unity_프로젝트_구조.md (accepted)
          docs/80_Jira_Git_운영_원칙/01_Jira_Git_컨벤션.md §9 -->

## 게이트키퍼 리뷰 필요

<!-- 해당 시 체크 -->
- [ ] NetworkProjectConfig / Fusion Runner / 세션 → 유민재(@dbalsend) / 최진우(@jk62362)
- [ ] Art / VFX / 캐릭터·맵 룩 / 발표용 비주얼 → 우수연(@goodyeon91)
- [ ] 폴더·네임스페이스 / 카드 시스템 구조 / 기준문서 / 카드 데이터 모델 → 박병준(@pbjuni1007)

## 체크리스트 (§11)

- [ ] 브랜치명 `{feature|fix}/[gameplay|network|visual|server|infra]/[1레벨 서브디렉토리]/A303-{n}-{slug}` 형식
- [ ] 커밋 메시지 전부 `[A303-{n}] [타입]: {제목}` 형식
- [ ] MR 제목에 `[A303-{n}]` 포함
- [ ] 도메인 구분자 라벨 지정 (`[GAMEPLAY]` 등)
- [ ] 공유 파일(`PlayerHealth`/`Gun`/`CardDeck`) 수정 시 관련 담당자 리뷰어 지정
- [ ] 씬/프리팹 수정 시 `[SCENE-LOCK]` Mattermost 공지 완료 (§15)
- [ ] Photon AppID/라이선스 키가 tracked 파일에 들어있지 않음 (§16)
- [ ] CI green
- [ ] 토론/코멘트 전부 resolve

/assign me
