namespace Colosseum.Network
{
    /// <summary>
    /// 서버-클라이언트 SemVer 호환성 검증 (서버/클라 공통).
    ///
    /// Major.Minor 일치 시 호환. Patch 차이는 허용.
    /// 개발 빌드(Editor / Debug build)에서는 검증을 bypass한다.
    /// </summary>
    public static class VersionValidator
    {
        /// <summary>
        /// 개발 빌드 여부. Editor 또는 Debug.isDebugBuild이면 true.
        /// </summary>
        public static bool IsDevBuild()
        {
#if UNITY_EDITOR
            return true;
#else
            return UnityEngine.Debug.isDebugBuild;
#endif
        }

        /// <summary>
        /// 두 버전의 Major.Minor가 일치하면 호환.
        /// 파싱 실패 시 false 반환 (비호환 취급).
        /// Lenient: "1.0" 같은 2-part도 "1.0.0"으로 처리.
        /// </summary>
        public static bool IsCompatible(string serverVersion, string clientVersion)
        {
            if (string.IsNullOrEmpty(serverVersion) || string.IsNullOrEmpty(clientVersion))
                return false;

            ParseMajorMinor(serverVersion, out int sMajor, out int sMinor);
            ParseMajorMinor(clientVersion, out int cMajor, out int cMinor);

            if (sMajor < 0 || cMajor < 0)
                return false;

            return sMajor == cMajor && sMinor == cMinor;
        }

        /// <summary>
        /// VERSION 프로토콜 메시지에서 버전 문자열 추출.
        /// "VERSION:0.1.0" → "0.1.0"
        /// </summary>
        public static string ParseVersionPayload(string message)
        {
            if (message != null && message.StartsWith("VERSION:"))
                return message.Substring(8);
            return null;
        }

        /// <summary>
        /// Major 버전 문자열 추출. Fusion AppVersion용 (세션 그룹핑).
        /// "0.1.0" → "0", 파싱 실패 시 원본 반환.
        /// </summary>
        public static string MajorVersion(string version)
        {
            // Lobby 세션 실행 에러로 잠시 주석 (병준이형이 수정 예정)
            //if (string.IsNullOrEmpty(version)) return version ?? "";
            //string[] parts = version.Split('.');
            //if (parts.Length >= 1 && int.TryParse(parts[0], out _))
            //    return parts[0];
            return version;
        }

        private static void ParseMajorMinor(string version, out int major, out int minor)
        {
            major = -1;
            minor = 0;

            if (string.IsNullOrEmpty(version)) return;

            string[] parts = version.Split('.');
            if (parts.Length < 1 || !int.TryParse(parts[0], out int m))
                return;
            major = m;

            if (parts.Length >= 2)
            {
                if (!int.TryParse(parts[1], out int n))
                {
                    major = -1;
                    return;
                }
                minor = n;
            }
        }
    }
}
