using System.Collections;
using UnityEngine;
using TMPro;
using Colosseum.Utils;

namespace Colosseum.UI
{
    /// <summary>
    /// A215 GO UI placeholder — advance 시점 시각 cue.
    ///
    /// 시나리오 lock v2 5 advance 시점에 0.5초 Text 표시.
    /// 디자이너 비동기 review backlog (placeholder, 일관성 추후 검토).
    ///
    /// hook 구조:
    ///   - RoomManager.OnAdvanceTriggered 이벤트 구독.
    ///   - 트리거 시 TextMeshProUGUI + CanvasGroup alpha fade in/out + 0.5초 표시.
    ///   - Coroutine 단일 인스턴스 (이전 cue 진행 중이면 cancel + restart).
    ///
    /// 씬 배치: Canvas → AdvanceCueUI GameObject에 이 컴포넌트 추가.
    ///   Inspector에서 _cueText(TextMeshProUGUI), _canvasGroup(CanvasGroup) 연결.
    ///   RoomManager는 Start()에서 SceneSearch로 자동 탐색.
    /// </summary>
    public class AdvanceCueUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _cueText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private string _cueMessage = "GO!";
        [SerializeField] private float _displayDurationSeconds = 0.5f;
        [SerializeField] private float _fadeDurationSeconds = 0.1f;

        private Coroutine _activeCue;
        private Game.RoomManager _roomManager;

        private void Start()
        {
            // Dedicated Server(headless)에서는 UI가 필요 없다.
            if (Application.isBatchMode) return;

            _roomManager = SceneSearch.FindInScene<Game.RoomManager>(gameObject.scene);
            if (_roomManager != null)
            {
                _roomManager.OnAdvanceTriggered += HandleAdvance;
            }
            else
            {
                Debug.LogWarning("[Colosseum] AdvanceCueUI: RoomManager not found in scene.");
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (_roomManager != null)
            {
                _roomManager.OnAdvanceTriggered -= HandleAdvance;
            }
        }

        private void HandleAdvance()
        {
            if (_activeCue != null) StopCoroutine(_activeCue);
            _activeCue = StartCoroutine(ShowCue());
        }

        private IEnumerator ShowCue()
        {
            if (_cueText != null) _cueText.text = _cueMessage;

            // Fade in
            yield return Fade(0f, 1f, _fadeDurationSeconds);
            // Hold
            yield return new WaitForSeconds(_displayDurationSeconds);
            // Fade out
            yield return Fade(1f, 0f, _fadeDurationSeconds);

            HideImmediate();
            _activeCue = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_canvasGroup == null || duration <= 0f)
            {
                if (_canvasGroup != null) _canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        private void HideImmediate()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }
    }
}
