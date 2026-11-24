"""Offline regression checks for the optional icon generator."""

import contextlib
import io
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image

from tools import make_brand


class IconGenerationTests(unittest.TestCase):
    def test_icons_build_without_wordmark_font_and_preserve_logos(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for folder in ("assets", "src", "docs"):
                (root / folder).mkdir()
            mark = root / "assets" / "g-mark.png"
            shutil.copyfile(make_brand.MARK_PATH, mark)
            logos = ("src/Lockup.png", "docs/logo-light.png", "docs/logo-dark.png")
            for logo in logos:
                (root / logo).write_bytes(b"existing finished logo artwork")

            # A wordmark lookup must not depend on fonts installed on this machine.
            # Keep the Windows font registry unavailable during the complete render.
            with (
                patch.object(make_brand, "ROOT", root),
                patch.object(make_brand, "MARK_PATH", mark),
                patch.object(sys, "argv", ["make_brand.py"]),
                patch.dict(sys.modules, {"winreg": None}),
                contextlib.redirect_stdout(io.StringIO()),
            ):
                make_brand.main()

            for logo in logos:
                self.assertEqual((root / logo).read_bytes(), b"existing finished logo artwork")
            for icon in ("Gravity.ico", "GravityPdf.ico"):
                with Image.open(root / "src" / icon) as image:
                    self.assertEqual(image.format, "ICO")
                    self.assertEqual(image.ico.sizes(), {(s, s) for s in make_brand.ICON_SIZES})
            for name, size in (("gravity-icon-1024.png", 1024), ("gravity-pdf-512.png", 512)):
                with Image.open(root / "assets" / name) as image:
                    self.assertEqual(image.size, (size, size))
                    self.assertIsNotNone(image.getbbox())


if __name__ == "__main__":
    unittest.main()
