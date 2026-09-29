// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VSDK.Views;
using System.Runtime.InteropServices;

namespace VSDK;

public partial class App : Application
{
    internal static string? PickMacExecutable(string title)
    {
        // Avalonia 12.1 classifies .app bundles as folders and drops them from file-picker results.
        var panel = Send(GetClass("NSOpenPanel"), Selector("openPanel"));
        var text = Marshal.StringToCoTaskMemUTF8(title);
        try
        {
            SendVoid(panel, Selector("setTitle:"), SendObject(GetClass("NSString"), Selector("stringWithUTF8String:"), text));
            return Send(panel, Selector("runModal")) == 1
                ? Marshal.PtrToStringUTF8(Send(Send(Send(panel, Selector("URL")), Selector("path")), Selector("UTF8String")))
                : null;
        }
        finally
        {
            SendVoid(panel, Selector("orderOut:"), 0);
            Marshal.FreeCoTaskMem(text);
        }
    }

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint SendObject(nint receiver, nint selector, nint value);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector, nint value);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(Program.Service());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
