#!/usr/bin/env python3
"""
DaxStudio Build Script
Usage:
    python build.py build          - Build the test project
    python build.py test           - Run all Visual Query Plan tests
    python build.py test <filter>  - Run tests matching filter
    python build.py run            - Launch DaxStudio
    python build.py rebuild        - Clean rebuild of test project
    python build.py restore        - Restore NuGet packages
"""

import subprocess
import sys
import os

MSBUILD = r"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
VSTEST = r"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"

REPO_ROOT = os.path.dirname(os.path.abspath(__file__))
TEST_PROJECT = os.path.join(REPO_ROOT, "tests", "DaxStudio.Tests", "DaxStudio.Tests.csproj")
TEST_DLL = os.path.join(REPO_ROOT, "src", "bin", "Debug", "DaxStudio.Tests.dll")
EXE_PATH = os.path.join(REPO_ROOT, "src", "bin", "Debug", "DaxStudio.exe")

def run_cmd(args, check=True):
    """Run a command and return the result."""
    print(f">>> {' '.join(args)}")
    result = subprocess.run(args, cwd=REPO_ROOT)
    if check and result.returncode != 0:
        sys.exit(result.returncode)
    return result

def build():
    """Build the test project."""
    run_cmd([MSBUILD, TEST_PROJECT, "-t:Build", "-p:Configuration=Debug", "-v:minimal", "-m"])

def rebuild():
    """Rebuild the test project."""
    run_cmd([MSBUILD, TEST_PROJECT, "-t:Rebuild", "-p:Configuration=Debug", "-v:minimal", "-m"])

def restore():
    """Restore NuGet packages."""
    run_cmd([MSBUILD, TEST_PROJECT, "-t:Restore", "-p:Configuration=Debug", "-v:minimal"])

def test(filter_pattern=None):
    """Run tests."""
    if not os.path.exists(TEST_DLL):
        print("Test DLL not found, building first...")
        build()

    args = [VSTEST, TEST_DLL]
    if filter_pattern:
        args.extend(["--TestCaseFilter:" + filter_pattern])
    else:
        args.extend(["--TestCaseFilter:FullyQualifiedName~VisualQueryPlan"])
    args.extend(["--logger:console;verbosity=minimal"])
    run_cmd(args, check=False)

def run_app():
    """Launch DaxStudio."""
    if not os.path.exists(EXE_PATH):
        print("DaxStudio.exe not found, building first...")
        build()
    subprocess.Popen([EXE_PATH], cwd=REPO_ROOT)
    print(f"Launched: {EXE_PATH}")

def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    cmd = sys.argv[1].lower()

    if cmd == "build":
        build()
    elif cmd == "rebuild":
        rebuild()
    elif cmd == "restore":
        restore()
    elif cmd == "test":
        filter_pattern = sys.argv[2] if len(sys.argv) > 2 else None
        test(filter_pattern)
    elif cmd == "run":
        run_app()
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)
        sys.exit(1)

if __name__ == "__main__":
    main()
