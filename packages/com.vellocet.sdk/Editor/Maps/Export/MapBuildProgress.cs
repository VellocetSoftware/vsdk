// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
using System;

namespace Vellocet.Sdk.Editor.Maps
{
    public interface IMapBuildProgressSink
    {
        void Report(float progress, string message);
        void Log(string message);
    }

    public sealed class DelegateMapBuildProgressSink : IMapBuildProgressSink
    {
        private readonly Action<string> _log;
        private readonly Action<float, string> _report;

        public DelegateMapBuildProgressSink(Action<float, string> report = null, Action<string> log = null)
        {
            _report = report;
            _log = log;
        }

        public void Report(float progress, string message)
        {
            _report?.Invoke(progress, message ?? string.Empty);
        }

        public void Log(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                _log?.Invoke(message.Trim());
        }
    }
}
#endif