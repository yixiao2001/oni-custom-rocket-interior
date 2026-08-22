#!/usr/bin/env bash
# 一键发布到 Steam 创意工坊（appid 457140，Oxygen Not Included）
#
# 原理：WSL 里构建打包 → 内容与 workshop.vdf 暂存到 Windows 侧
#       Documents/oni-upload/ → 通过 WSL interop 调用 Windows 版 steamcmd 上传。
#
# 用法：
#   ./publish.sh                    # 构建+打包+生成 vdf，打印上传命令
#   ./publish.sh --upload <Steam用户名>  # 直接上传（需输入密码 / Steam Guard）
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
  PUBLISHEDFILEID=$(grep -oP '(?<="publishedfileid"\s+")\d+' "$VDF" || echo 0)
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
  echo "== 上传中（按提示输入密码与 Steam Guard 码）=="
  LOG="$UPLOAD_DIR/steamcmd.log"
  cd "$UPLOAD_DIR"
  /mnt/c/Windows/System32/cmd.exe /c "steamcmd.exe +login $2 +workshop_build_item workshop.vdf +quit" 2>&1 | tee "$LOG"

  if [ "$PUBLISHEDFILEID" = "0" ]; then
    NEW_ID=$(grep -aoP '(?<=Uploaded new item ID: )\d+' "$LOG" | tail -1 || true)
    if [ -n "${NEW_ID:-}" ]; then
      sed -i 's/"publishedfileid"\t*"[^"]*"/"publishedfileid"\t\t\t\t"'"$NEW_ID"'"/' "$VDF"
      echo "已把 publishedfileid=$NEW_ID 写回 workshop.vdf（以后更新会复用）"
    fi
  fi
  echo "完成。物品页: https://steamcommunity.com/sharedfiles/filedetails/?id=${NEW_ID:-$PUBLISHEDFILEID}"
else
  cat <<TIP

一切就绪。上传方式二选一：

A) 本目录执行:  ./publish.sh --upload <你的Steam用户名>

B) 打开 Windows CMD 执行:
  cd C:\\Users\\$WIN_USER\\Documents\\oni-upload
  steamcmd.exe +login <你的Steam用户名> +workshop_build_item workshop.vdf +quit

首次会要求输入密码和 Steam Guard 验证码；需要该 Steam 账号拥有缺氧本体。
TIP
fi
