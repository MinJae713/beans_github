namespace Colosseum.Network
{
    /// <summary>
    /// 닉네임 작명 규칙 (서버/클라 공통).
    ///
    /// 규칙 (D??? 결정):
    /// - 허용 문자: 한글(가-힣), 영문 대소문자(a-z, A-Z), 숫자(0-9), 언더바(_)
    /// - 한글 포함 시 길이 2~8자
    /// - 한글 미포함(영문/숫자/_ 만) 시 길이 1~16자
    /// - 공백 / 그 외 특수문자 / 한자 / 일본어 등 모두 거절
    /// </summary>
    public static class NicknameValidator
    {
        public const int KoreanMinLength = 2;
        public const int KoreanMaxLength = 8;
        public const int AsciiMinLength = 1;
        public const int AsciiMaxLength = 16;

        public enum Result
        {
            Ok,
            Empty,
            InvalidChar,
            TooShort,
            TooLong,
        }

        /// <summary>
        /// 닉네임 검증. 통과하면 Result.Ok 반환.
        /// </summary>
        public static Result Validate(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return Result.Empty;

            bool hasKorean = false;
            foreach (char c in nickname)
            {
                bool ok = false;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_')
                {
                    ok = true;
                }
                else if (c >= '가' && c <= '힣')
                {
                    ok = true;
                    hasKorean = true;
                }
                if (!ok) return Result.InvalidChar;
            }

            int len = nickname.Length;
            if (hasKorean)
            {
                if (len < KoreanMinLength) return Result.TooShort;
                if (len > KoreanMaxLength) return Result.TooLong;
            }
            else
            {
                if (len < AsciiMinLength) return Result.TooShort;
                if (len > AsciiMaxLength) return Result.TooLong;
            }
            return Result.Ok;
        }

        /// <summary>
        /// 검증 결과를 ERROR 메시지의 sub-code 로 변환.
        /// 서버가 클라에게 보내는 ERROR:nickname_xxx 와 일치.
        /// </summary>
        public static string ToErrorCode(Result r)
        {
            switch (r)
            {
                case Result.Empty:       return "nickname_empty";
                case Result.InvalidChar: return "nickname_invalid_char";
                case Result.TooShort:    return "nickname_too_short";
                case Result.TooLong:     return "nickname_too_long";
                default:                 return "nickname_invalid";
            }
        }

        /// <summary>
        /// 사용자에게 보여줄 한국어 안내 메시지. UI에서 그대로 띄우거나 자체 번역.
        /// </summary>
        public static string ToKoreanMessage(Result r)
        {
            switch (r)
            {
                case Result.Empty:
                    return "닉네임을 입력해주세요.";
                case Result.InvalidChar:
                    return "한글, 영문, 숫자, 언더바(_)만 사용할 수 있어요.";
                case Result.TooShort:
                    return $"한글은 {KoreanMinLength}자 이상, 영문/숫자는 {AsciiMinLength}자 이상이어야 해요.";
                case Result.TooLong:
                    return $"한글은 {KoreanMaxLength}자 이하, 영문/숫자는 {AsciiMaxLength}자 이하로 입력해주세요.";
                default:
                    return "닉네임이 올바르지 않아요.";
            }
        }
    }
}
