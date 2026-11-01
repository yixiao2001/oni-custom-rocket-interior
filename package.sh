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
dotnet build src/CustomRocketInterior.csproj -c Release -p:ONI_MODS_DEV_DIR=/nonexistent
dotnet run --project tests/RegressionTests.csproj -c Release
dotnet run --project tests/RegressionTests.csproj -c Release --no-build -- \
  --check-mod src/bin/Release/netstandard2.1/CustomRocketInterior.dll

echo "== stage v$VERSION =="
rm -rf "$STAGE" && mkdir -p "$STAGE"
cp src/bin/Release/netstandard2.1/CustomRocketInterior.dll "$STAGE/"
cp src/mod.yaml src/mod_info.yaml "$STAGE/"
cp LICENSE PLib-LICENSE.txt "$STAGE/"
# 翻译文件（PLib 的 PLocalization 从 <mod>/translations/<code>.po 加载）
if [ -d src/translations ]; then
    mkdir -p "$STAGE/translations"
    cp src/translations/*.po "$STAGE/translations/"
fi

echo "== zip =="
mkdir -p "$OUT"
rm -f "$OUT/CustomRocketInterior-v$VERSION.zip"
python3 - <<EOF
import zipfile, os
with zipfile.ZipFile("$OUT/CustomRocketInterior-v$VERSION.zip", "w", zipfile.ZIP_DEFLATED) as z:
    for root, dirs, files in os.walk("$STAGE"):
        dirs.sort()
        for f in sorted(files):
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, "$OUT"))
print("packed:", "$OUT/CustomRocketInterior-v$VERSION.zip")
EOF

echo "完成。上传用文件: $OUT/CustomRocketInterior-v$VERSION.zip"
