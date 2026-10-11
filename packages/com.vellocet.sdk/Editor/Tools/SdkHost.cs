// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

#if UNITY_EDITOR
namespace Vellocet.Sdk.Editor.Tools
{
    public interface ISdkHostContext
    {
        bool IsBaseProject { get; }
        string ModeName { get; }
    }

    public static class SdkHost
    {
        private static readonly ISdkHostContext DefaultContext = new DefaultSdkHostContext();
        private static ISdkHostContext _context;

        public static ISdkHostContext Context => _context ?? DefaultContext;
        public static bool IsBaseProject => Context.IsBaseProject;
        public static string ModeName => string.IsNullOrWhiteSpace(Context.ModeName) ? "Add-on" : Context.ModeName;

        public static void RegisterContext(ISdkHostContext context)
        {
            _context = context;
        }

        public static void ClearContext()
        {
            _context = null;
        }

        private sealed class DefaultSdkHostContext : ISdkHostContext
        {
            public bool IsBaseProject => false;
            public string ModeName => "Add-on";
        }
    }
}
#endif