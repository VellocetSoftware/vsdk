// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Vellocet.Sdk.Editor
{
    [InitializeOnLoad]
    internal static class SdkUnityVersionGuard
    {
        internal const string RequiredUnityVersion = Constants.RequiredUnityVersion;

        private const string DialogSessionKeyPrefix = "Vellocet.Sdk.Editor.UnityVersionDialog.";
        private static bool _startupCheckScheduled;

        static SdkUnityVersionGuard()
        {
            ScheduleStartupCheck();
        }

        internal static bool IsSupported =>
            string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.OrdinalIgnoreCase);

        internal static string RequirementMessage =>
            $"The Vellocet SDK requires Unity {RequiredUnityVersion} exactly. " +
            $"This project is open in Unity {Application.unityVersion}. Open the project with the required Editor " +
            "version in Unity Hub before compiling or building SDK content.";

        [MenuItem("Tools/Vellocet/SDK/Check Unity Version", priority = 504)]
        private static void CheckFromMenu()
        {
            if (IsSupported)
            {
                EditorUtility.DisplayDialog(
                    "Vellocet SDK Unity Version",
                    $"Unity {Application.unityVersion} is the supported Vellocet SDK version.",
                    "OK");
                return;
            }

            ShowMismatchDialog();
        }

        internal static bool TryValidateForOperation(string operation, out string error)
        {
            if (IsSupported)
            {
                error = null;
                return true;
            }

            error = string.IsNullOrWhiteSpace(operation)
                ? RequirementMessage
                : $"{operation} is blocked. {RequirementMessage}";
            Debug.LogError("[Vellocet SDK] " + error);

            if (!Application.isBatchMode)
                ShowMismatchDialog(operation);

            return false;
        }

        private static void ScheduleStartupCheck()
        {
            if (_startupCheckScheduled)
                return;

            _startupCheckScheduled = true;
            EditorApplication.update += CheckWhenEditorReady;
        }

        private static void CheckWhenEditorReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= CheckWhenEditorReady;
            _startupCheckScheduled = false;

            if (IsSupported)
                return;

            Debug.LogError("[Vellocet SDK] " + RequirementMessage);
            if (Application.isBatchMode)
                return;

            var sessionKey = BuildDialogSessionKey();
            if (SessionState.GetBool(sessionKey, false))
                return;

            SessionState.SetBool(sessionKey, true);
            ShowMismatchDialog();
        }

        private static void ShowMismatchDialog(string operation = null)
        {
            var prefix = string.IsNullOrWhiteSpace(operation)
                ? string.Empty
                : $"{operation} cannot continue.\n\n";
            EditorUtility.DisplayDialog(
                "Unsupported Unity Version",
                prefix + RequirementMessage,
                "OK");
        }

        private static string BuildDialogSessionKey()
        {
            var projectId = Hash128.Compute(Application.dataPath).ToString();
            return $"{DialogSessionKeyPrefix}{projectId}.{Application.unityVersion}.{RequiredUnityVersion}";
        }
    }
}
#endif