using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Colosseum.Card;

namespace Colosseum.UI
{
    /// <summary>
    /// 보유 카드 슬롯 hover 시 화면 좌하/우하 인벤토리 위쪽에 등장하는 카드 풀-뷰 팝업.
    /// CardInventoryHUD가 슬롯 등록 시 등장/숨김 + 내용 갱신을 호출. 등급별 색상은 인벤토리와 통일.
    /// CardSelectionUI 의 prefab/메서드는 직접 참조하지 않고 시각만 같게 자체 빌드 (영역 분리).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardHoverPopup : MonoBehaviour
    {
        // 인벤토리와 통일된 등급 색
        private static readonly Color CommonColor = new Color(0.64f, 0.57f, 0.48f, 0.85f);
        private static readonly Color RareColor = new Color(0.71f, 0.77f, 1f, 0.85f);
        private static readonly Color EpicColor = new Color(0.72f, 0.35f, 1f, 0.85f);
        private static readonly Color LegendaryColor = new Color(0.91f, 0.77f, 0f, 0.85f);
        private static readonly Color FrameBgColor = new Color(0.10f, 0.10f, 0.11f, 0.85f);
        private static readonly Color WhiteText = Color.white;
        private static readonly Color SubduedText = new Color(0.85f, 0.85f, 0.85f, 1f);

        private const float PopupWidth = 280f;
        private const float PopupHeight = 400f;

        private const float FrameOutlineThickness = 4f;
        private const float GradeStripHeight = 36f;
        private const float IconSize = 140f;
        private const float Padding = 12f;

        private RectTransform _rect;
        private Image _frame;
        private Outline _frameOutline;
        private Image _gradeStrip;
        private TextMeshProUGUI _gradeLabel;
        private Image _iconImage;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _descText;

        /// <summary>
        /// playerSlot: 0=P1(좌하), 1=P2(우하). popupBottomY: 인벤토리 최상단에서 팝업 하단까지 y(local).
        /// whiteSprite: 인벤토리에서 사용한 흰 스프라이트 재사용.
        /// </summary>
        public void Build(int playerSlot, Sprite whiteSprite, float popupBottomY)
        {
            _rect = GetComponent<RectTransform>();
            bool mirrored = (playerSlot == 1);
            float ax = mirrored ? 1f : 0f;
            _rect.anchorMin = new Vector2(ax, 0f);
            _rect.anchorMax = new Vector2(ax, 0f);
            _rect.pivot = new Vector2(ax, 0f);
            _rect.anchoredPosition = new Vector2(0f, popupBottomY);
            _rect.sizeDelta = new Vector2(PopupWidth, PopupHeight);

            // Frame 배경 + Outline
            _frame = gameObject.AddComponent<Image>();
            _frame.sprite = whiteSprite;
            _frame.color = FrameBgColor;
            _frameOutline = gameObject.AddComponent<Outline>();
            _frameOutline.effectDistance = new Vector2(FrameOutlineThickness, -FrameOutlineThickness);
            _frameOutline.effectColor = CommonColor;

            // 상단 grade strip
            GameObject stripObj = new GameObject("GradeStrip");
            stripObj.transform.SetParent(transform, false);
            _gradeStrip = stripObj.AddComponent<Image>();
            _gradeStrip.sprite = whiteSprite;
            _gradeStrip.color = CommonColor;
            RectTransform stripRect = stripObj.GetComponent<RectTransform>();
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.anchoredPosition = Vector2.zero;
            stripRect.sizeDelta = new Vector2(0f, GradeStripHeight);

            _gradeLabel = CreateText(stripObj.transform, "GradeLabel", "COMMON", 18f, FontStyles.Bold, WhiteText, TextAlignmentOptions.Center);
            RectTransform glr = _gradeLabel.GetComponent<RectTransform>();
            glr.anchorMin = Vector2.zero; glr.anchorMax = Vector2.one;
            glr.offsetMin = Vector2.zero; glr.offsetMax = Vector2.zero;

            // 아이콘
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(transform, false);
            _iconImage = iconObj.AddComponent<Image>();
            _iconImage.preserveAspect = true;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -(GradeStripHeight + Padding));
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);

            // 카드 이름
            _nameText = CreateText(transform, "Name", "", 22f, FontStyles.Bold, WhiteText, TextAlignmentOptions.Center);
            RectTransform nr = _nameText.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 1f);
            nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0f, -(GradeStripHeight + Padding + IconSize + Padding));
            nr.sizeDelta = new Vector2(-Padding * 2f, 32f);

            // 설명
            _descText = CreateText(transform, "Desc", "", 14f, FontStyles.Normal, SubduedText, TextAlignmentOptions.TopLeft);
            _descText.enableWordWrapping = true;
            RectTransform dr = _descText.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0f, 0f);
            dr.anchorMax = new Vector2(1f, 1f);
            dr.pivot = new Vector2(0.5f, 0.5f);
            dr.offsetMin = new Vector2(Padding, Padding);
            dr.offsetMax = new Vector2(-Padding, -(GradeStripHeight + Padding + IconSize + Padding + 32f + Padding));

            gameObject.SetActive(false);
        }

        public void Show(CardData card)
        {
            if (card == null) { Hide(); return; }

            Color rc = GetRarityColor(card.rarity);
            _frameOutline.effectColor = rc;
            _gradeStrip.color = rc;
            _gradeLabel.text = GetRarityLabel(card.rarity);
            _iconImage.sprite = card.cardIcon;
            _iconImage.enabled = card.cardIcon != null;
            _nameText.text = card.cardName;
            _descText.text = card.description;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }

        private Color GetRarityColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return CommonColor;
                case CardRarity.Rare: return RareColor;
                case CardRarity.Epic: return EpicColor;
                case CardRarity.Legendary: return LegendaryColor;
                default: return CommonColor;
            }
        }

        private string GetRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return "COMMON";
                case CardRarity.Rare: return "RARE";
                case CardRarity.Epic: return "EPIC";
                case CardRarity.Legendary: return "LEGENDARY";
                default: return "COMMON";
            }
        }
    }
}
