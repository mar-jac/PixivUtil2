# -*- mode: python ; coding: utf-8 -*-
"""Single-file PyInstaller backend build for the Windows trial shell."""

from pathlib import Path

from PyInstaller.utils.hooks import (
    collect_data_files,
    collect_dynamic_libs,
    collect_submodules,
)


ROOT = Path.cwd()


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

hiddenimports = [
    "html5lib",
    "html5lib.treebuilders.etree",
    "html5lib.treewalkers.etree",
    "socks",
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

a = Analysis(
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
pyz = PYZ(a.pure)
exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name="PixivUtil2",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
