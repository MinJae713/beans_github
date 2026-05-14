#if UNITY_EDITOR
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Colosseum.Card;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 전용 에셋 팩 ScriptableObject.
    /// Unity 에디터에서 Assets/_Project/Data/DevMode/ 에 인스턴스를 생성한다.
    /// Create Asset Menu: Colosseum > DevMode > Asset Pack
    /// </summary>
    [CreateAssetMenu(fileName = "DevModeAssetPack", menuName = "Colosseum/DevMode/Asset Pack")]
    public class DevModeAssetPack : ScriptableObject
    {
        [Header("캐릭터")]
        [Tooltip("DevMode에서 스폰할 플레이어 프리팹 (NetworkPrefabRef — PrefabTable 등록 필수)")]
        public NetworkPrefabRef playerPrefab;

        [Header("카드")]
        [Tooltip("DevMode 덱에 사용할 CardData 목록")]
        public List<CardData> cardDataList = new List<CardData>();

        [Header("총알 / 발사체")]
        [Tooltip("DevMode에서 사용할 기본 탄환 프리팹 (NetworkPrefabRef — PrefabTable 등록 필수)")]
        public NetworkPrefabRef bulletPrefab;

        [Header("UI 스프라이트")]
        [Tooltip("HUD·UI 오버레이에 쓸 스프라이트 목록 (순서: DevMode UI 담당자와 협의)")]
        public List<Sprite> uiSprites = new List<Sprite>();

        [Header("머터리얼")]
        [Tooltip("DevMode 전용 override 머터리얼 목록")]
        public List<Material> materials = new List<Material>();

        [Header("사운드")]
        [Tooltip("DevMode에서 재생할 AudioClip 목록")]
        public List<AudioClip> audioClips = new List<AudioClip>();
    }
}
#endif
