// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;

namespace Vellocet.Sdk.Logging
{
    public enum SdkConsoleLogLevel
    {
        Info,
        Warning,
        Error
    }

    public interface ISdkConsoleSink
    {
        void Write(SdkConsoleLogLevel level, string message, string category);
    }

    public static class SdkConsoleBridge
    {
        private static ISdkConsoleSink _sink;

        public static bool HasSink => _sink != null;

        public static void RegisterSink(ISdkConsoleSink consoleSink)
        {
            _sink = consoleSink;
        }

        public static void ClearSink(ISdkConsoleSink consoleSink = null)
        {
            if (consoleSink == null || ReferenceEquals(_sink, consoleSink))
                _sink = null;
        }

        public static void Write(string message, string category = null)
        {
            Write(SdkConsoleLogLevel.Info, message, category);
        }

        public static void Warn(string message, string category = null)
        {
            Write(SdkConsoleLogLevel.Warning, message, category);
        }

        public static void Error(string message, string category = null)
        {
            Write(SdkConsoleLogLevel.Error, message, category);
        }

        public static void Write(SdkConsoleLogLevel level, string message, string category = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var activeSink = _sink;
            if (activeSink != null)
                try
                {
                    activeSink.Write(level, message.Trim(), NormalizeCategory(category));
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SDK] Console sink failed: {ex.Message}");
                }

            WriteToUnityConsole(level, message.Trim(), NormalizeCategory(category));
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        }

        private static void WriteToUnityConsole(SdkConsoleLogLevel level, string message, string category)
        {
            var line = string.IsNullOrWhiteSpace(category) ? message : $"[{category}] {message}";
            switch (level)
            {
                case SdkConsoleLogLevel.Warning:
                    Debug.LogWarning(line);
                    break;
                case SdkConsoleLogLevel.Error:
                    Debug.LogError(line);
                    break;
                case SdkConsoleLogLevel.Info:
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}