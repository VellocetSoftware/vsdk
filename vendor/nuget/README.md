# Vellocet UI package feed

This repository pins the same immutable shared UI package as the other Vellocet
apps. Source, gallery, design foundations and release instructions live in
`vellocet-ui`. Common styles must be changed there rather than copied here.

`NuGet.Config` maps only `Vellocet.UI.Avalonia` to this feed; other packages use
nuget.org. Keep this archive in source control so a clean checkout is buildable.

| Package | SHA-256 |
| --- | --- |
| `Vellocet.UI.Avalonia.0.4.1.nupkg` | `9aa6f8605ca34702068d6c2bd9a93d50411275964fdfc99c21e93cfeb4ccfda6` |

Update the archive, exact package reference, lock files (where used) and checksum
together. Published versions are immutable. The package includes the Vellocet
license and Tabler icon notices.
