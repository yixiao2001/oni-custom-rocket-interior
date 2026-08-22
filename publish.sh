#!/usr/bin/env bash
# 一键发布到 Steam 创意工坊（appid 457140，Oxygen Not Included）
#
# 重要经验：绝不能把【需要交互】的 steamcmd 输出重定向到文件——
# C 运行时对文件输出做块缓冲，password:/Steam Guard 等提示会滞留缓冲区
# 不显示，看起来就像"回车无效"。所以：
#   登录（--login）→ 弹出窗口、无重定向、全程可见，只需成功一次；
#   上传（--upload）→ 凭据已缓存后完全非交互，才允许重定向抓日志。
#
# 用法：
#   ./publish.sh                              # 只构建打包并生成 vdf
#   ./publish.sh --login <Steam用户名> [密码]  # 首次登录（弹窗交互，缓存凭据）
#   ./publish.sh --upload <Steam用户名>        # 非交互上传/更新创意工坊物品
set -euo pipefail
cd "$(dirname "$0")"

VERSION=$(grep -oP '(?<=<Version>)[^<]+' src/CustomRocketInterior.csproj)
WIN_USER=${WIN_USER:-xiaoyi}
UPLOAD_DIR="/mnt/c/Users/$WIN_USER/Documents/oni-upload"
STAGE_WIN='C:\Users\xiaoyi\Documents\oni-upload\CustomRocketInterior'
CMD_EXE=/mnt/c/Windows/System32/cmd.exe

TITLE="Custom Rocket Interior 自定义火箭舱内空间"
# 富文本描述优先取 workshop-description.txt（BBCode 多行），否则用内置单行
if [ -f workshop-description.txt ]; then
  DESCRIPTION=$(cat workshop-description.txt)
else
  DESCRIPTION="自定义太空员舱内部空间大小与墙体材质（宽高12-96格，钢/火成岩/中子质/玻璃）。Customize rocket habitat interior size (12-96 tiles) and wall material. 需要眼冒金星DLC / Spaced Out required. 源码 Source: https://github.com/yixiao2001/oni-custom-rocket-interior"
fi

prepare() {
  echo "== 构建与打包 =="
  ./package.sh

  echo "== 暂存干净内容到 $UPLOAD_DIR =="
  mkdir -p "$UPLOAD_DIR"
  rm -rf "$UPLOAD_DIR/CustomRocketInterior"
  cp -r release/CustomRocketInterior "$UPLOAD_DIR/"
  cp preview.png "$UPLOAD_DIR/" 2>/dev/null || true

  if [ ! -f "$UPLOAD_DIR/steamcmd.exe" ]; then
    echo "== 下载 Windows 版 steamcmd =="
    curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip -o "$UPLOAD_DIR/steamcmd.zip"
    (cd "$UPLOAD_DIR" && python3 -c "import zipfile; zipfile.ZipFile('steamcmd.zip').extractall('.')")
  fi

  # 生成 workshop.vdf（保留已有 publishedfileid 以便更新同一物品）
  PUBLISHEDFILEID=0
  VDF="$UPLOAD_DIR/workshop.vdf"
  [ -f "$VDF" ] && PUBLISHEDFILEID=$(grep -oP '"publishedfileid"\s+"\K\d+' "$VDF" || echo 0)
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
  echo "workshop.vdf 就绪 (publishedfileid=$PUBLISHEDFILEID)"
}

case "${1:-}" in
"--login")
  [ -n "${2:-}" ] || { echo "用法: $0 --login <Steam用户名> [密码]"; exit 1; }
  prepare
  PASS_ARG=""
  [ -n "${3:-}" ] && PASS_ARG="$3"
  # 无重定向！让 password / Steam Guard 提示全部实时可见。
  # 注意：批处理文件必须纯 ASCII —— 中文版 cmd 按 GBK 解析 .cmd，
  # 写入 UTF-8 中文会被拆成乱码并破坏整行命令（'gged' 不是内部或外部命令）。
  cat > "$UPLOAD_DIR/run-login.cmd" <<BAT
@echo off
cd /d "%~dp0"
title steamcmd login - $2
steamcmd.exe +login $2 $PASS_ARG +quit
echo.
echo ===== If you saw "Logged in OK" above, credentials are cached. Close this window. =====
pause
BAT
  cd "$UPLOAD_DIR"
  "$CMD_EXE" /c start "" /wait run-login.cmd
  echo "登录流程结束。之后发版只需:  ./publish.sh --upload $2"
  ;;

"--upload")
  [ -n "${2:-}" ] || { echo "用法: $0 --upload <Steam用户名>"; exit 1; }
  prepare

  cd "$UPLOAD_DIR"
  echo "== 探测缓存的登录凭据 =="
  set +e
  timeout 45 "$CMD_EXE" /c "steamcmd.exe +login $2 +quit >login-probe.log 2>&1"
  RC=$?
  set -e
  if [ $RC -ne 0 ] || ! grep -aqE 'Logged in OK|user info\.\.\. *OK' login-probe.log; then
    echo "未检测到有效登录（可能凭据未缓存或已过期）。请先执行一次："
    echo "  ./publish.sh --login $2"
    echo "或手动打开 CMD 在 oni-upload 目录运行:"
    echo "  steamcmd.exe +login $2 +quit"
    exit 1
  fi
  echo "凭据有效，开始非交互上传..."

  set +e
  timeout 900 "$CMD_EXE" /c "steamcmd.exe +login $2 +workshop_build_item workshop.vdf >steamcmd.log 2>&1"
  set -e

  PUBLISHEDFILEID=$(grep -oP '"publishedfileid"\s+"\K\d+' workshop.vdf || echo 0)
  NEW_ID=""
  [ -f steamcmd.log ] && NEW_ID=$(grep -aoP '(?<=Uploaded new item ID: )\d+' steamcmd.log | tail -1 || true)
  if [ -n "$NEW_ID" ]; then
    sed -i 's/"publishedfileid"\t*"[^"]*"/"publishedfileid"\t\t\t\t"'"$NEW_ID"'"/' workshop.vdf
    echo "已把 publishedfileid=$NEW_ID 写回 workshop.vdf（以后更新复用）"
  fi
  echo "完成。物品页: https://steamcommunity.com/sharedfiles/filedetails/?id=${NEW_ID:-$PUBLISHEDFILEID}"
  echo "详细日志: $UPLOAD_DIR/steamcmd.log"
  ;;

*)
  prepare
  cat <<TIP

下一步：
  首次使用先登录一次（弹窗交互，全程可见）：
    ./publish.sh --login <你的Steam用户名>
  之后发版/更新一条命令：
    ./publish.sh --upload <你的Steam用户名>

也可以自己开 CMD 手动登录（效果相同）：
  cd C:\\Users\\$WIN_USER\\Documents\\oni-upload
  steamcmd.exe +login <你的Steam用户名> +quit
TIP
  ;;
esac
