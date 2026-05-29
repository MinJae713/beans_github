# DevMode Asset Pack 인스턴스 저장소

Unity 에디터에서 다음 순서로 기본 인스턴스를 생성하세요:

1. Project 창에서 이 폴더(`Assets/_Project/Data/DevMode/`) 선택
2. 우클릭 → Create → Colosseum → DevMode → Asset Pack
3. 파일명: `DefaultAssetPack`
4. Inspector에서 각 슬롯(playerPrefab, cardDataList, bulletPrefab 등)에 에셋 할당
5. DevMode 씬의 `DevModeController` 게임오브젝트 → Inspector의 `Asset Pack` 슬롯에 드래그

## 슬롯 요약

| 슬롯 | 타입 | 용도 |
|------|------|------|
| playerPrefab | GameObject | DevMode 스폰 플레이어 프리팹 |
| cardDataList | List<CardData> | DevMode 디버그 패널에 노출할 카드. `DefaultAssetPack`은 `docs/40_카드_무기_설계/10_세부설계/03_MVP_카드_후보_카탈로그.md`의 MVP16 16장만 순서대로 포함한다. |
| bulletPrefab | GameObject | 기본 탄환 프리팹 |
| uiSprites | List<Sprite> | HUD·UI 오버레이 스프라이트 |
| materials | List<Material> | Override 머터리얼 |
| audioClips | List<AudioClip> | 재생할 사운드 클립 |
