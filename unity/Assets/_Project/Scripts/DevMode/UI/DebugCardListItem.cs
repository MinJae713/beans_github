using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Colosseum.Card;

namespace Colosseum.UI
{
    /// <summary>
    /// DebugCardSelectPanel 카드 목록 1개 row 컴포넌트.
    ///
    /// A303-270:
    ///   - 카드별 누적 적용 횟수 배지(×N) 노출용 countBadge 필드 추가
    ///   - 클릭 후 selected 토글 효과 잔존 방지 (EventSystem 디셀렉트 + Navigation None)
    /// </summary>
    public class DebugCardListItem : MonoBehaviour
    {
        public Image cardIcon;
        public TextMeshProUGUI cardName;
        public TextMeshProUGUI countBadge;   // A303-270: 누적 적용 횟수 ×N 배지
        public Button applyButton;
        public Button addToDeckButton;

        private CardData _data;
        private System.Action<CardData> _onApply;
        private System.Action<CardData> _onAddToDeck;

        public void Setup(CardData data, System.Action<CardData> onApply, System.Action<CardData> onAddToDeck)
        {
            _data = data;
            _onApply = onApply;
            _onAddToDeck = onAddToDeck;

            if (cardName != null)
                cardName.text = data != null ? data.cardName : "—";

            if (cardIcon != null)
            {
                if (data != null && data.cardIcon != null)
                {
                    cardIcon.sprite = data.cardIcon;
                    cardIcon.color = Color.white;
                }
                else
                {
                    cardIcon.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                }
            }

            // A303-270: 초기 카운트 0 (배지 숨김)
            SetCount(0);

            if (applyButton != null)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApplyClicked);
                // A303-270: Navigation 자체를 끄면 자동 selected 효과도 제거 — 토글처럼 잔존하지 않음
                var nav = applyButton.navigation;
                nav.mode = Navigation.Mode.None;
                applyButton.navigation = nav;
            }

            if (addToDeckButton != null)
            {
                addToDeckButton.onClick.RemoveAllListeners();
                addToDeckButton.onClick.AddListener(OnAddToDeckClicked);
                var nav = addToDeckButton.navigation;
                nav.mode = Navigation.Mode.None;
                addToDeckButton.navigation = nav;
            }
        }

        /// <summary>
        /// A303-270: 누적 적용 횟수 배지 갱신. count > 0 일 때만 ×N 표시.
        /// </summary>
        public void SetCount(int count)
        {
            if (countBadge == null) return;
            if (count > 0)
            {
                countBadge.text = $"×{count}";
                countBadge.gameObject.SetActive(true);
            }
            else
            {
                countBadge.text = string.Empty;
                countBadge.gameObject.SetActive(false);
            }
        }

        private void OnApplyClicked()
        {
            _onApply?.Invoke(_data);
            // A303-270: 클릭 직후 EventSystem 포커스 해제 — 버튼이 selected highlight로 잔존하지 않게
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnAddToDeckClicked()
        {
            _onAddToDeck?.Invoke(_data);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
