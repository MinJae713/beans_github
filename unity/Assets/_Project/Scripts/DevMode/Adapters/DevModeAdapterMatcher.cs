#if UNITY_EDITOR
using System;

namespace Colosseum.DevMode
{
    /// <summary>
    /// DevMode 어댑터 공통 매칭 헬퍼.
    /// "devmode-{category}-{index}" 명명 규칙에서 index를 추출한다.
    /// AdapterMatcherTests (EditMode) + 어댑터 3종이 공유.
    /// </summary>
    public static class DevModeAdapterMatcher
    {
        /// <summary>
        /// objectName이 prefix로 시작하고, 그 뒤에 음수가 아닌 정수 index가 오는지 파싱한다.
        /// </summary>
        /// <param name="objectName">GameObject.name</param>
        /// <param name="prefix">예: "devmode-material-", "devmode-ui-", "devmode-audio-"</param>
        /// <param name="index">파싱 성공 시 0 이상의 정수. 실패 시 -1.</param>
        /// <returns>파싱 성공 여부</returns>
        public static bool TryParseIndex(string objectName, string prefix, out int index)
        {
            index = -1;

            if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(prefix))
                return false;

            if (!objectName.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            var suffix = objectName.Substring(prefix.Length);

            // suffix가 비어있거나, 순수 숫자가 아닌 경우 실패
            if (string.IsNullOrEmpty(suffix))
                return false;

            if (!int.TryParse(suffix, out int parsed))
                return false;

            // 음수 거부
            if (parsed < 0)
                return false;

            index = parsed;
            return true;
        }
    }
}
#endif
