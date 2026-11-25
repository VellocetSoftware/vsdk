# Vellocet UI package feed

This repository pins the same immutable shared UI package as the other Vellocet
apps. Source, gallery, design foundations and release instructions live in
`vellocet-ui`. Common styles must be changed there rather than copied here.

`NuGet.Config` maps only `Vellocet.UI.Avalonia` to this feed; other packages use
nuget.org. Keep this archive in source control so a clean checkout is buildable.

| Package | SHA-256 |
| --- | --- |
| `Vellocet.UI.Avalonia.0.7.0.nupkg` | `6088fbe904f265518d166f0d5af6c469588d7610b8b8f00c2010871913851e86` |

Update the archive, exact package reference, lock files (where used) and checksum
together. Published versions are immutable. The package includes the Vellocet
license and Tabler icon notices.
