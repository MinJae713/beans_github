using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Colosseum.Player;

namespace Colosseum.UI
{
    /// <summary>
    /// 단일 플레이어의 HP 바 표시 컴포넌트.
    /// fill 비율은 PlayerHealth.OnHealthChangedEvent를 구독하여 변동 시점에만 갱신된다.
    /// LateUpdate는 위치 추적 + 사망 시 숨김만 담당 (fill 폴링 없음).
    /// A303-327: 바 위에 닉네임 라벨을 함께 표시. 닉네임 텍스트 갱신은 HPBarManager가 SetNickname으로 주입.
    /// </summary>
    public class PlayerHPBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _fillImage;
        [SerializeField] private TMP_Text _nicknameLabel;

        [Header("Settings")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1.6f, 0f);
        // 최대 체력은 PlayerHealth.MaxHealth (= GameplayProfile.combat.baseline.maxHealth)에서 읽는다.
        // 중복 SerializeField 제거 — 단일 진실.

        private PlayerHealth _playerHealth;
        private Transform _target;
        private SpriteRenderer _spriteRenderer;
        private Camera _cam;

        /// <summary>
        /// HPBarManager가 인스턴스 생성 직후 호출. PlayerHealth 참조 + fillImage / nickname 주입 + 이벤트 구독.
        /// </summary>
        public void Init(PlayerHealth health, Image fillImage, TMP_Text nicknameLabel)
        {
            _playerHealth = health;
            _fillImage = fillImage;
            _nicknameLabel = nicknameLabel;
            _target = health.transform;
            _spriteRenderer = health.GetComponent<SpriteRenderer>();
            _cam = Camera.main;

            // HP 변동 이벤트 구독 — fill 갱신을 LateUpdate 폴링에서 콜백으로 전환.
            _playerHealth.OnHealthChangedEvent += UpdateFill;

            // 초기 fill 한 번 반영 (현재 CurrentHealth 기준)
            UpdateFill(_playerHealth);
        }

        /// <summary>
        /// HPBarManager가 닉네임 갱신 시 호출. null/empty 들어오면 빈 문자열로 처리.
        /// </summary>
        public void SetNickname(string nickname)
        {
            if (_nicknameLabel == null) return;
            _nicknameLabel.text = string.IsNullOrEmpty(nickname) ? string.Empty : nickname;
        }

        private void OnDestroy()
        {
            // 이벤트 누수 방지 — 구독 해제. Domain Reload 비활성 환경 대비.
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChangedEvent -= UpdateFill;
            }
        }

        /// <summary>
        /// PlayerHealth.OnHealthChangedEvent 발생 시 호출. fill 비율 + 색상 갱신.
        /// </summary>
        private void UpdateFill(PlayerHealth player)
        {
            if (_fillImage == null) return;

            float maxHealth = player.MaxHealth;
            float ratio = maxHealth > 0f ? Mathf.Clamp01(player.CurrentHealth / maxHealth) : 0f;
            _fillImage.fillAmount = ratio;

            // HP 비율에 따라 색 변경
            if (ratio > 0.5f)
                _fillImage.color = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
            else
                _fillImage.color = Color.Lerp(Color.red, Color.yellow, ratio * 2f);
        }

        private void LateUpdate()
        {
            if (_playerHealth == null || _target == null)
            {
                Destroy(gameObject);
                return;
            }

            // 죽었으면 바 + 닉네임 함께 숨기기
            if (_playerHealth.IsDead)
            {
                _fillImage.transform.parent.gameObject.SetActive(false);
                if (_nicknameLabel != null) _nicknameLabel.gameObject.SetActive(false);
                return;
            }
            else
            {
                _fillImage.transform.parent.gameObject.SetActive(true);
                if (_nicknameLabel != null) _nicknameLabel.gameObject.SetActive(true);
            }

            // 월드 좌표 → 스크린 좌표로 변환 (캐릭터 transform 추적)
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            float centerX = (_spriteRenderer != null)
                ? _spriteRenderer.bounds.center.x
                : _target.position.x;
            Vector3 worldPos = new Vector3(centerX, _target.position.y, _target.position.z) + _offset;
            Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
            transform.position = screenPos;

            // fill 갱신은 OnHealthChangedEvent에서 처리 — LateUpdate에서 폴링하지 않음.
        }
    }
}
