# Custom Rocket Interior（缺氧：自定义火箭舱内空间）

[English](README.en.md) | 简体中文

自定义《缺氧 (Oxygen Not Included)》**太空员舱**等火箭模块的**内部空间大小与墙体材质**。

适配版本：U59-744825（本体 + 《眼冒金星！》DLC）

## 功能

- 🚀 **舱室大小自由定制**：宽度 / 高度各 12–96 格（原版太空员舱仅 12×11），房间自动填满整个火箭内部世界；
- 🧱 **墙体材质四选一**：钢 / 火成岩 / 中子质（不可破坏）/ 玻璃，同时作用于墙砖建筑与背板格子；
- 🌫️ **全图去迷雾**：火箭内部世界不再有圆形迷雾残留；
- ⚙️ **游戏内设置界面**（基于 PLib），修改后**新建的火箭立即生效**，无需重启；
- 🚪 出入口自动吸附到左下角，气体/液体端口、控制站等功能建筑自动嵌入新墙体布局。

| 默认 40×40 | 原版 12×11 |
|---|---|
| 约 10 倍可用面积 | 胶囊形小房间 |

## 安装

### 方式一：Steam 创意工坊（推荐）
直接订阅：[steamcommunity.com/sharedfiles/filedetails/?id=3789310279](https://steamcommunity.com/sharedfiles/filedetails/?id=3789310279)

### 方式二：手动安装
1. 从 [Releases](../../releases) 下载最新 zip；
2. 解压到游戏本地 mod 目录下的 `CustomRocketInterior` 子文件夹：
   - Windows: `文档/Klei/OxygenNotIncluded/mods/Local/`
   - Linux/macOS: `~/.config/unity3d/Klei/Oxygen Not Included/mods/Local/`
3. 主菜单 → 模组 → 启用本 mod。

> 需要启用《眼冒金星！》DLC；纯本体模式下本 mod 会正确显示为不兼容。

## 游戏内设置

主菜单 → 模组 → Custom Rocket Interior → **设置**：

| 选项 | 范围 | 默认 | 说明 |
|---|---|---|---|
| 舱室宽度 | 12–96 格 | 40 | 即火箭内部世界宽度 |
| 舱室高度 | 12–96 格 | 40 | 顶部多留 1 格空隙（液体管道不能靠近世界顶边），实际可用高度 = 设置值 − 3 |
| 墙体材质 | 钢 / 火成岩 / 中子质 / 玻璃 | 钢 | 外壳墙砖建造材质 + 背板格子元素 |

- 每次新建火箭内部前实时读取配置，改完设置后**新建的火箭立即生效**；
- 尺寸越大，全局网格能同时容纳的火箭内部越少，超限时游戏会提示 No free rocket interior；
- 只影响**新建**的火箭；已建成的火箭内部已在存档中固化。

## 工作原理

基于对 `Assembly-CSharp.dll` 的反编译分析：

1. 火箭内部世界尺寸由 `TUNING.ROCKETRY.ROCKET_INTERIOR_SIZE`（public static 可变字段，默认 32×32）控制，直接赋值即可；
2. 舱内布局来自 YAML 内部模板（如 `expansion1::interiors/habitat_medium`，12×11），经 `TemplateCache.GetTemplate` 加载并缓存——Harmony Postfix 在返回前把模板程序化重塑为居中矩形：清空全部壳层、功能建筑吸附到新墙线、重建边界并真空填充；
3. 原版只在放置模板后做一次以中心为圆点的圆形迷雾揭示，方形世界四角会残留——Postfix 对世界逐格完全揭示。

更详细的机制解析见源码注释（`src/Core/InteriorResizer.cs` 头部有完整说明）。

## 开发

```bash
./build.sh      # 编译 + 自动部署到 Dev mod 目录（需先配置游戏路径）
./package.sh    # 打包可上传的创意工坊 zip 到 release/
```
> ⚠️ **发布必须使用官方工具**：Steam 库 → 工具 → "Oxygen Not Included - Mod Uploader"。
> 用 steamcmd 直接上传会产生游戏无法读取的散装内容格式，导致所有订阅者"下载失败"。

- 编译需 .NET SDK 8 与游戏本体；游戏路径通过环境变量 `ONI_MANAGED_DIR` / `ONI_MODS_DEV_DIR` 或直接改 csproj 配置；
- 反编译查源码：`ilspycmd -t 类名 <Managed目录>/Assembly-CSharp.dll`；
- 项目结构与设计原则见下方目录树与代码注释。

### 发布到 Steam 创意工坊

ONI 客户端没有内置 Steam 上传器，本仓库用 Valve 官方 steamcmd 管线发布：

```bash
./publish.sh --upload <你的Steam用户名>
```

脚本会自动构建、打包、把干净内容与 workshop.vdf 暂存到 Windows 侧
`Documents/oni-upload/` 并调用 steamcmd 上传。首次运行会创建新物品并要求
输入密码与 Steam Guard 验证码；之后自动复用 publishedfileid 进行版本更新。

```
src/
├── Mod.cs                        # 入口（读选项 → 套用配置 → 注册设置界面）
├── Options/RocketInteriorOptions.cs  # PLib 设置项（含中文枚举下拉框）
├── Config/InteriorSizeConfig.cs  # 运行时配置
├── Core/InteriorResizer.cs       # 核心纯逻辑：模板重塑
└── Patches/
    ├── TemplateCachePatch.cs         # 拦截模板加载并重塑
    ├── RevealInteriorWorldPatch.cs   # 内部世界全图去迷雾
    └── ApplyLiveOptionsPatch.cs      # 建火箭前实时应用最新选项
```

## 已知限制

- 只对新建火箭生效；外景贴图仍是原版大小（视觉问题，不影响玩法）；
- 小地图上的火箭足迹多边形会随新墙体坐标扩大，可能与外景不一致；
- 最外圈墙体是普通建筑（中子质除外），复制人可以挖掘；挖开后即世界边缘的太空真空。

## 许可证

MIT — 详见 [LICENSE](LICENSE)。
