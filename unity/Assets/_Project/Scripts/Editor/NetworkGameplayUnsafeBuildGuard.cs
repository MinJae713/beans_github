#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Colosseum.Editor
{
    public sealed class NetworkGameplayUnsafeBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            NetworkGameplayUnsafeBuildGuard.ValidateOrThrow();
        }
    }

    public static class NetworkGameplayUnsafeBuildGuard
    {
        private static readonly Regex[] ForbiddenPatterns =
        {
            new Regex(@"\bunsafe\b", RegexOptions.Compiled),
            new Regex(@"\bstackalloc\b", RegexOptions.Compiled),
            new Regex(@"\bfixed\s*\(", RegexOptions.Compiled),
            new Regex(@"\bdelegate\s*\*", RegexOptions.Compiled),
            new Regex(@"\b(?:byte|sbyte|short|ushort|int|uint|long|ulong|char|float|double|decimal|bool|void|IntPtr|UIntPtr)\s*\*", RegexOptions.Compiled),
            new Regex(@"->", RegexOptions.Compiled),
        };

        public static void ValidateOrThrow()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts", "NetworkGameplay");
            ValidateOrThrow(root);
        }

        public static void ValidateOrThrow(string root)
        {
            if (!Directory.Exists(root))
                throw new BuildFailedException($"NetworkGameplay source root missing: {root}");

            var offenders = new List<string>();
            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = StripCommentsAndStrings(File.ReadAllText(path));
                foreach (Regex pattern in ForbiddenPatterns)
                {
                    if (!pattern.IsMatch(source))
                        continue;

                    string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    offenders.Add($"{relative}: {pattern}");
                    break;
                }
            }

            if (offenders.Count == 0)
                return;

            throw new BuildFailedException(
                "Colosseum.NetworkGameplay has allowUnsafeCode=true only for Fusion IL Weaver generated accessors. " +
                "Hand-written unsafe code must move to a smaller reviewed assembly. Offenders: " +
                string.Join(", ", offenders));
        }

        private static string StripCommentsAndStrings(string source)
        {
            var result = new StringBuilder(source.Length);
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;
            bool inVerbatimString = false;

            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (current == '\n')
                    {
                        inLineComment = false;
                        result.Append(current);
                    }
                    else
                    {
                        result.Append(' ');
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        result.Append("  ");
                        i++;
                    }
                    else
                    {
                        result.Append(current == '\n' ? '\n' : ' ');
                    }
                    continue;
                }

                if (inString)
                {
                    if (inVerbatimString && current == '"' && next == '"')
                    {
                        result.Append("  ");
                        i++;
                        continue;
                    }

                    bool endsString = current == '"' && (inVerbatimString || !IsEscaped(source, i));
                    if (endsString)
                    {
                        inString = false;
                        inVerbatimString = false;
                    }

                    result.Append(current == '\n' ? '\n' : ' ');
                    continue;
                }

                if (inChar)
                {
                    if (current == '\'' && !IsEscaped(source, i))
                        inChar = false;

                    result.Append(current == '\n' ? '\n' : ' ');
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    inLineComment = true;
                    result.Append("  ");
                    i++;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    result.Append("  ");
                    i++;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    inString = true;
                    inVerbatimString = true;
                    result.Append("  ");
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    result.Append(' ');
                    continue;
                }

                if (current == '\'')
                {
                    inChar = true;
                    result.Append(' ');
                    continue;
                }

                result.Append(current);
            }

            return result.ToString();
        }

        private static bool IsEscaped(string source, int quoteIndex)
        {
            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;

            return slashCount % 2 == 1;
        }
    }
}
#endif
