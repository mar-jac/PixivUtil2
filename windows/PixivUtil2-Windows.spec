# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller build for the native Windows PixivUtil2 bundle.

Build from a Windows shell with:
    py -3.11 -m PyInstaller --clean --noconfirm windows/PixivUtil2-Windows.spec

Set PIXIVUTIL_FFMPEG to the full path of ffmpeg.exe to include ugoira
conversion support in the generated one-folder bundle.
"""

import os
from pathlib import Path

from PyInstaller.utils.hooks import (
    collect_data_files,
    collect_dynamic_libs,
    collect_submodules,
)


ROOT = Path(SPECPATH).parent.parent
ICON = ROOT / "icon2.ico"


def collect_datas(package_name):
    try:
        return collect_data_files(package_name)
    except Exception:
        return []


def collect_libs(package_name):
    try:
        return collect_dynamic_libs(package_name)
    except Exception:
        return []


def collect_hidden(package_name):
    try:
        return collect_submodules(package_name)
    except Exception:
        return []


datas = [
    (str(ROOT / "content_provider.json"), "."),
    (str(ROOT / "novel_template.html"), "."),
    (str(ROOT / "README.md"), "."),
    (str(ROOT / "requirements.txt"), "."),
    (str(ROOT / "template.html"), "."),
]

for package in ("browser_cookie3", "certifi", "cloudscraper", "curl_cffi", "PIL"):
    datas += collect_datas(package)

binaries = []
for package in ("curl_cffi", "PIL"):
    binaries += collect_libs(package)

ffmpeg_path = os.environ.get("PIXIVUTIL_FFMPEG")
if ffmpeg_path:
    ffmpeg = Path(ffmpeg_path)
    if not ffmpeg.is_file():
        raise SystemExit(f"PIXIVUTIL_FFMPEG does not point to a file: {ffmpeg}")
    binaries.append((str(ffmpeg), "."))

hiddenimports = [
    "html5lib",
    "html5lib.treebuilders.etree",
    "html5lib.treewalkers.etree",
    "socks",
    "tkinter",
    "tkinter.filedialog",
    "tkinter.messagebox",
    "tkinter.scrolledtext",
    "tkinter.ttk",
]

for package in (
    "browser_cookie3",
    "bs4",
    "cloudscraper",
    "common",
    "curl_cffi",
    "demjson3",
    "handler",
    "mechanize",
    "model",
    "PIL",
):
    hiddenimports += collect_hidden(package)


a_cli = Analysis(
    [str(ROOT / "PixivUtil2.py")],
    pathex=[str(ROOT)],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=["pytest", "test", "test_data"],
    noarchive=False,
    optimize=0,
)
pyz_cli = PYZ(a_cli.pure)
exe_cli = EXE(
    pyz_cli,
    a_cli.scripts,
    [],
    exclude_binaries=True,
    name="PixivUtil2",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=str(ICON) if ICON.exists() else None,
)

a_gui = Analysis(
    [str(ROOT / "PixivUtilGUI.py")],
    pathex=[str(ROOT)],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=["pytest", "test", "test_data"],
    noarchive=False,
    optimize=0,
)
pyz_gui = PYZ(a_gui.pure)
exe_gui = EXE(
    pyz_gui,
    a_gui.scripts,
    [],
    exclude_binaries=True,
    name="PixivUtilGUI",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=str(ICON) if ICON.exists() else None,
)

coll = COLLECT(
    exe_cli,
    exe_gui,
    a_cli.binaries,
    a_gui.binaries,
    a_cli.zipfiles,
    a_gui.zipfiles,
    a_cli.datas,
    a_gui.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="PixivUtil2-Windows",
)
