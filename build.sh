#!/usr/bin/env bash
# 编译并把 mod 部署到游戏的 Dev mod 目录（csproj 里已配置自动部署，此脚本提供带清理的一键构建）
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

# 可用环境变量覆盖游戏路径：
#   ONI_MANAGED_DIR  游戏 Managed 目录
#   ONI_MODS_DEV_DIR 本地开发 mod 目录
dotnet build src/CustomRocketInterior.csproj -c Release "$@"

echo
echo "部署完成。启动游戏即可在 主菜单 -> 模组 中看到 Custom Rocket Interior。"
echo "日志: /mnt/c/Users/xiaoyi/AppData/LocalLow/Klei/Oxygen Not Included/player.log"
