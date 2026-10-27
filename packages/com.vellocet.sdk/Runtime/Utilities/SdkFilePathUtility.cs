using System;
using System.IO;

namespace Vellocet.Sdk.Utilities
{
    internal static class SdkFilePathUtility
    {
        internal static string ForIo(string path)
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            var fullPath = Path.GetFullPath(path).Replace('/', '\\');
            if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal)) return fullPath;
            return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + fullPath.Substring(2)
                : @"\\?\" + fullPath;
#else
            return path;
#endif
        }
    }
}
