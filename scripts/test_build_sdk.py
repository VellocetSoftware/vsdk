import importlib.util
import json
from pathlib import Path
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
            for runtime in build.RUNTIMES:
                executable = root / "Editor" / runtime / ("Vertex.exe" if runtime == "win-x64" else "Vertex")
                executable.parent.mkdir(parents=True)
                executable.write_bytes(b"editor")
            self.assertEqual(build.validate_vertex(root)["revision"], "abc")
            executable.write_bytes(b"")
            with self.assertRaisesRegex(ValueError, "Missing Vertex"):
                build.validate_vertex(root)
            executable.unlink()
            executable.symlink_to(root / "Editor/win-x64/Vertex.exe")
            with self.assertRaisesRegex(ValueError, "symbolic links"):
                build.validate_vertex(root)


if __name__ == "__main__":
    unittest.main()
