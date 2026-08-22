#!/usr/bin/env bash
# 一键发布到 Steam 创意工坊（appid 457140，Oxygen Not Included）
#
# 原理：WSL 里构建打包 → 内容与 workshop.vdf 暂存到 Windows 侧
#       Documents/oni-upload/ → 弹出真实 Windows 控制台窗口运行 steamcmd 上传。
#
# 注意：不能通过 WSL 管道直接跑 steamcmd——stdout 被接管后，
#       密码回显与回车输入都会失效，必须在真实控制台里交互。
#
# 用法：
#   ./publish.sh                    # 构建+打包+生成 vdf，打印上传命令
#   ./publish.sh --upload <Steam用户名>                # 弹出控制台窗口上传（交互输密码）
#   ./publish.sh --upload <Steam用户名> <密码>          # 免输密码（Steam Guard 码仍需手输）
#
# 首次运行创建新创意工坊物品；之后自动复用 publishedfileid 进行更新。
set -euo pipefail
cd "$(dirname "$0")"

VERSION=$(grep -oP '(?<=<Version>)[^<]+' src/CustomRocketInterior.csproj)
WIN_USER=${WIN_USER:-xiaoyi}
UPLOAD_DIR="/mnt/c/Users/$WIN_USER/Documents/oni-upload"
STAGE_WIN='C:\Users\xiaoyi\Documents\oni-upload\CustomRocketInterior'

TITLE="Custom Rocket Interior 自定义火箭舱内空间"
DESCRIPTION="自定义太空员舱内部空间大小与墙体材质。宽高各 12-96 格自由调节（原版仅 12x11），墙体支持钢/火成岩/中子质/玻璃，内置去迷雾，游戏内设置界面即时生效（对新建火箭）。需要《眼冒金星！》DLC。源码: https://github.com/yixiao2001/oni-custom-rocket-interior | Customize habitat interior size (12-96 tiles per axis) and wall material (Steel/Igneous Rock/Neutronium/Glass). Spaced Out required. Source: https://github.com/yixiao2001/oni-custom-rocket-interior"

echo "== 1/4 构建与打包 =="
./package.sh

echo "== 2/4 暂存干净内容到 $UPLOAD_DIR =="
mkdir -p "$UPLOAD_DIR"
rm -rf "$UPLOAD_DIR/CustomRocketInterior"
cp -r release/CustomRocketInterior "$UPLOAD_DIR/"
cp preview.png "$UPLOAD_DIR/" 2>/dev/null || echo "(提示: 无 preview.png，可稍后在 Steam 网页端补传预览图)"

echo "== 3/4 准备 Windows 版 steamcmd =="
if [ ! -f "$UPLOAD_DIR/steamcmd.exe" ]; then
  echo "下载 steamcmd..."
  curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip -o "$UPLOAD_DIR/steamcmd.zip"
  (cd "$UPLOAD_DIR" && python3 -c "import zipfile; zipfile.ZipFile('steamcmd.zip').extractall('.')")
fi

echo "== 4/4 生成 workshop.vdf =="
PUBLISHEDFILEID=0
VDF="$UPLOAD_DIR/workshop.vdf"
if [ -f "$VDF" ]; then
  PUBLISHEDFILEID=$(grep -oP '"publishedfileid"\s+"\K\d+' "$VDF" || echo 0)
fi

cat > "$VDF" <<EOF
"workshopitem"
{
	"appid"						"457140"
	"publishedfileid"			"$PUBLISHEDFILEID"
	"contentfolder"				"$STAGE_WIN"
	"title"						"$TITLE"
	"description"				"$DESCRIPTION"
	"visibility"				"0"
	"changenote"				"v$VERSION"
}
EOF
echo "已生成 $VDF (publishedfileid=$PUBLISHEDFILEID)"

if [ "${1:-}" = "--upload" ] && [ -n "${2:-}" ]; then
  PASS_ARG=""
  if [ -n "${3:-}" ]; then
    # 免交互变体：密码直接写进命令行（注意：若密码含 % 需写成 %%）
    PASS_ARG="\"$3\""
    echo "== 使用命令行传入的密码（跳过密码输入）=="
  fi
  echo "== 弹出 Windows 控制台窗口上传（可能需要输入 Steam Guard 验证码）=="
  echo "   小贴士：输验证码前先切到英文键盘(Win+空格)；不要在窗口内点击选中文字，"
  # 生成批处理：日志重定向到文件；stdin 保持真实控制台
  cat > "$UPLOAD_DIR/run-upload.cmd" <<BAT
@echo off
cd /d "%~dp0"
steamcmd.exe +login $2 $PASS_ARG +workshop_build_item workshop.vdf >steamcmd.log 2>&1
echo.
echo ===== Upload finished. Full log below =====
type steamcmd.log
echo.
pause
BAT
  cd "$UPLOAD_DIR"
  # start 开独立控制台窗口，/wait 等 steamcmd 结束再回来解析结果
  /mnt/c/Windows/System32/cmd.exe /c start "" /wait run-upload.cmd

  NEW_ID=""
  if [ -f steamcmd.log ]; then
    NEW_ID=$(grep -aoP '(?<=Uploaded new item ID: )\d+' steamcmd.log | tail -1 || true)
  fi
  if [ -n "$NEW_ID" ] && [ "$PUBLISHEDFILEID" = "0" ]; then
    sed -i 's/"publishedfileid"\t*"[^"]*"/"publishedfileid"\t\t\t\t"'"$NEW_ID"'"/' "$VDF"
    echo "已把 publishedfileid=$NEW_ID 写回 workshop.vdf（以后更新会复用）"
  fi
  echo "完成。物品页: https://steamcommunity.com/sharedfiles/filedetails/?id=${NEW_ID:-$PUBLISHEDFILEID}"
else
  cat <<TIP

一切就绪。上传方式二选一：

A) 本目录执行:  ./publish.sh --upload <你的Steam用户名>
   （会弹出一个 Windows 控制台窗口，在其中输入密码与 Steam Guard 验证码）

B) 自己开一个 Windows CMD 执行:
  cd C:\\Users\\$WIN_USER\\Documents\\oni-upload
  steamcmd.exe +login <你的Steam用户名> +workshop_build_item workshop.vdf +quit

首次会要求输入密码和 Steam Guard 验证码；需要该 Steam 账号拥有缺氧本体。
TIP
fi
