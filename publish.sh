#!/usr/bin/env bash
# Prepare a clean directory for Klei's official ONI Mod Uploader. Raw steamcmd loose-file
# uploads are incompatible with ONI's workshop archive format.
set -euo pipefail
cd "$(dirname "$0")"
if [ "$#" -gt 0 ]; then
  echo "Usage: ./publish.sh"
  echo "This script prepares a release. Update item 3789310279 in the official ONI Mod Uploader."
  exit 1
fi
./package.sh
VERSION=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' src/CustomRocketInterior.csproj)
echo
echo "Ready: $(pwd)/release/CustomRocketInterior-v$VERSION.zip"
echo "Steam Library -> Tools -> Oxygen Not Included Mod Uploader"
echo "Select Custom Rocket Interior (3789310279) -> Edit -> set data directory to release/CustomRocketInterior/ -> publish update."
echo "Keep the existing title, description, preview and visibility. Do not create a new item."
