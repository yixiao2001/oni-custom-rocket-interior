#!/usr/bin/env bash
# 打包可直接上传 Steam 创意工坊的 mod zip 到 release/ 目录
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

VERSION=$(grep -oP '(?<=<Version>)[^<]+' src/CustomRocketInterior.csproj)
OUT="release"
STAGE="$OUT/CustomRocketInterior"

echo "== build =="
dotnet build src/CustomRocketInterior.csproj -c Release

echo "== stage v$VERSION =="
rm -rf "$STAGE" && mkdir -p "$STAGE"
cp src/bin/Release/net48/CustomRocketInterior.dll "$STAGE/"
cp src/bin/Release/net48/PLib.dll "$STAGE/" 2>/dev/null || echo "WARN: PLib.dll not found in output"
cp src/mod.yaml src/mod_info.yaml "$STAGE/"

echo "== zip =="
mkdir -p "$OUT"
rm -f "$OUT/CustomRocketInterior-v$VERSION.zip"
python3 - <<EOF
import zipfile, os
with zipfile.ZipFile("$OUT/CustomRocketInterior-v$VERSION.zip", "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk("$STAGE"):
        for f in files:
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, "$OUT"))
print("packed:", "$OUT/CustomRocketInterior-v$VERSION.zip")
EOF

echo "完成。上传用文件: $OUT/CustomRocketInterior-v$VERSION.zip"
