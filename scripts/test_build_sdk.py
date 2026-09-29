import importlib.util
import json
from pathlib import Path
import plistlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("build_sdk", Path(__file__).with_name("build-sdk.py"))
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)


class ToolkitCheck(unittest.TestCase):
    def test_reject_incomplete_or_linked_editors(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "vertex-build.json").write_text(json.dumps({"revision": "abc", "runtimes": build.RUNTIMES}))
            (root / "Libraries").mkdir()
            for name in ("Vertex.Core.dll", "Vertex.Contracts.dll", "Vertex.VSig.dll"):
                (root / "Libraries" / name).write_bytes(b"library")
            for runtime in build.RUNTIMES:
                executable = root / "Editor" / runtime / ("Vertex.exe" if runtime == "win-x64" else "Vertex.app/Contents/MacOS/Vertex")
                executable.parent.mkdir(parents=True)
                executable.write_bytes(b"editor")
                if runtime.startswith("osx-"):
                    contents = executable.parent.parent
                    (contents / "Info.plist").write_bytes(plistlib.dumps({
                        "CFBundleIdentifier": "com.vellocet.vertex", "CFBundleExecutable": "Vertex", "CFBundleIconFile": "Vertex.icns"
                    }))
                    (contents / "Resources").mkdir()
                    (contents / "Resources/Vertex.icns").write_bytes(b"icon")
            self.assertEqual(build.validate_vertex(root)["revision"], "abc")
            icon = contents / "Resources/Vertex.icns"
            icon.unlink()
            with self.assertRaisesRegex(ValueError, "identity or icon"):
                build.validate_vertex(root)
            icon.write_bytes(b"icon")
            # Flattening a macOS app must no longer produce a distributable toolkit.
            loose = root / "Editor/osx-x64/Vertex"
            executable.rename(loose)
            with self.assertRaisesRegex(ValueError, "Missing Vertex executable"):
                build.validate_vertex(root)
            loose.rename(executable)
            executable.write_bytes(b"")
            with self.assertRaisesRegex(ValueError, "Missing Vertex"):
                build.validate_vertex(root)
            executable.unlink()
            executable.symlink_to(root / "Editor/win-x64/Vertex.exe")
            with self.assertRaisesRegex(ValueError, "symbolic links"):
                build.validate_vertex(root)


if __name__ == "__main__":
    unittest.main()
