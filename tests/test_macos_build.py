"""Offline packaging regressions using a stub macOS toolchain."""

import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def find_bash():
    if os.name == "nt":
        git = shutil.which("git")
        if git:
            candidate = Path(git).resolve().parent.parent / "bin" / "bash.exe"
            if candidate.is_file():
                return str(candidate)
        return None
    return shutil.which("bash")


STUB = r'''#!/bin/bash
set -euo pipefail
case "${0##*/}" in
  uname) printf 'Darwin\n' ;;
  xcrun)
    if [[ "$1" == --sdk ]]; then printf '/sdk\n'; exit; fi
    if [[ "${FAIL_AT:-}" == compile ]]; then exit 7; fi
    while [[ "$1" != -o ]]; do shift; done
    cp "$0" "$2"
    chmod +x "$2"
    ;;
  lipo)
    while [[ "$1" != -output ]]; do shift; done
    cp "$0" "$2"
    chmod +x "$2"
    ;;
  sips) while [[ "$1" != --out ]]; do shift; done; printf 'image' > "$2" ;;
  iconutil) while [[ "$1" != -o ]]; do shift; done; printf 'icon' > "$2" ;;
  codesign) if [[ "${FAIL_AT:-}" == signing ]]; then exit 8; fi ;;
  ditto)
    if [[ "${FAIL_AT:-}" == packaging ]]; then exit 9; fi
    shift 3
    (cd "$1"; find . -type f | sort) > "$2"
    ;;
  Gravity) if [[ "${FAIL_AT:-}" == tests ]]; then exit 10; fi ;;
  mv)
    if [[ "${FAIL_AT:-}" == install && "$1" == */Gravity.app && "$2" == */bin/Gravity.app ]]; then exit 12; fi
    exec /bin/mv "$@"
    ;;
  MakeMacIcon|plutil) ;;
  *) exit 11 ;;
esac
'''


@unittest.skipUnless(find_bash(), "Bash is required for build-script regressions")
class MacBuildTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="gravity build ")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.tools = self.root / "toolchain"
        self.tools.mkdir()
        for name in ("uname", "xcrun", "lipo", "sips", "iconutil", "plutil", "codesign", "ditto", "mv"):
            path = self.tools / name
            path.write_text(STUB, encoding="utf-8", newline="\n")
            path.chmod(0o755)
        shutil.copyfile(ROOT / "build-macos.sh", self.root / "build-macos.sh")
        for name in (
            "macos/App.swift", "macos/AppIcon.swift", "macos/Info.plist",
            "tools/MakeMacIcon.swift", "LICENSE", "THIRD_PARTY_NOTICES.md",
            "assets/g-mark.png", "assets/gravity-icon-1024.png", "assets/gravity-pdf-512.png",
            "src/Lockup.png", "third_party/lucide/icons/save.svg", "third_party/lucide/LICENSE",
        ):
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("fixture", encoding="utf-8")
        self.app = self.root / "bin" / "Gravity.app"
        self.executable = self.app / "Contents" / "MacOS" / "Gravity"
        self.executable.parent.mkdir(parents=True)
        self.executable.write_text("previous executable", encoding="utf-8")
        self.stale = self.app / "Contents" / "Resources" / "obsolete.txt"
        self.stale.parent.mkdir(parents=True)
        self.stale.write_text("stale output", encoding="utf-8")
        self.unrelated = self.root / "bin" / "keep.txt"
        self.unrelated.write_text("unrelated output", encoding="utf-8")
        self.archive = self.root / "dist" / "Gravity-1.0.0-mac-universal.zip"
        self.archive.parent.mkdir()
        self.archive.write_text("previous archive", encoding="utf-8")

    def build(self, failure=""):
        env = os.environ.copy()
        env["FAIL_AT"] = failure
        # The shell resolves the toolchain relative to cwd on both POSIX and Git Bash.
        command = 'export PATH="$PWD/toolchain:$PATH"; bash build-macos.sh --test --package'
        result = subprocess.run(
            [find_bash(), "-c", command], cwd=self.root, env=env,
            capture_output=True, text=True, timeout=60, check=False,
        )
        output = result.stdout + result.stderr
        for value in (str(self.root), self.root.as_posix()):
            output = output.replace(value, "<build fixture>")
        return result.returncode, output

    def assert_clean_workspace(self):
        self.assertEqual(list((self.root / "bin").glob("macos-build.*")), [])
        self.assertEqual(self.unrelated.read_text(), "unrelated output")

    def test_rebuild_excludes_stale_bundle_files(self):
        code, output = self.build()
        self.assertEqual(code, 0, output)
        self.assertFalse(self.stale.exists())
        self.assertTrue((self.app / "Contents" / "Resources" / "GravityPdf.icns").is_file())
        manifest = self.archive.read_text()
        self.assertIn("./Contents/MacOS/Gravity", manifest)
        self.assertNotIn("obsolete.txt", manifest)
        self.assert_clean_workspace()

    def test_failure_preserves_previous_app_and_archive(self):
        for stage, expected in (("compile", 7), ("signing", 8), ("tests", 10), ("packaging", 9), ("install", 12)):
            with self.subTest(stage=stage):
                code, output = self.build(stage)
                self.assertEqual(code, expected, output)
                self.assertEqual(self.executable.read_text(), "previous executable")
                self.assertEqual(self.stale.read_text(), "stale output")
                self.assertEqual(self.archive.read_text(), "previous archive")
                self.assert_clean_workspace()


if __name__ == "__main__":
    unittest.main()
