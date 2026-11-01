# Custom Rocket Interior（缺氧：自定义火箭舱内空间）

[English](README.en.md) | 简体中文

自定义《缺氧 (Oxygen Not Included)》**太空员舱**等火箭模块的**内部空间大小与墙体材质**。

适配版本：U59-744825（本体 + 《眼冒金星！》DLC）

## 功能

- 🚀 **舱室大小自由定制**：宽度 / 高度各 12–96 格（原版太空员舱仅 12×11），房间自动填满整个火箭内部世界；
- 🧱 **墙体材质四选一**：钢 / 火成岩 / 中子质 / 玻璃，同时作用于墙砖建筑与背板格子；
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
| 舱室高度 | 12–96 格 | 40 | 高度同上 |
| 墙体材质 | 钢 / 火成岩 / 中子质 / 玻璃 | 钢 | 外壳墙砖建造材质 + 背板格子元素 |

- 设置界面跟随游戏语言：默认英文，游戏语言为简体中文时显示中文（`translations/zh.po`）；
- 每次新建火箭内部前实时读取配置，改完设置后**新建的火箭立即生效**；
- 尺寸越大，全局网格能同时容纳的火箭内部越少，超限时游戏会提示 No free rocket interior；
- 只影响**新建**的火箭；已建成的火箭内部已在存档中固化。

## 可拆墙体与自建墙安装（v0.4.0）

“建造”分类新增两个默认开启的开关，修改后需要重启游戏：

- **允许拆除火箭内墙**：舱壳和窗砖可使用正常拆除工具拆除，包括中子质材质。四个固定气液端口、舱门和控制站不会被此选项解锁。已有舱室在读档时生效，不用重造火箭；顶墙允许按普通砖的可达方向从舱内拆除；已拆掉的墙不会因关闭开关而自动复原。
- **输入输出组件可依附任意实心砖**：气体、液体、固体的货舱输入／输出组件和电力插座，可依附火箭内部的普通砖、隔热砖、金属砖、窗砖等自建实心砖。保持原版旋转方向与相邻支撑位置，不支持天然格、网格砖、透气砖或悬空放置。舱外建筑规则不变。

拆掉支撑砖后组件显示缺少支撑，货舱交换停止；电力插座也断开与火箭电路的连接。补回有效实心砖后恢复。四个固定气液端口仍留在原位置，不会随重建的墙移动；可建造的货舱组件通过所属火箭访问货舱，不依赖这四个端口的位置。

拆墙只释放格子，不扩大世界边界。请先建好替代外壁，再拆旧墙，以维持气密性。关闭“任意实心砖”可恢复原版仅允许火箭舱壳支撑的安装规则。

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

- 编译需 .NET SDK 8 与 U59 游戏本体（目标 netstandard2.1）；游戏路径通过环境变量 `ONI_MANAGED_DIR` / `ONI_MODS_DEV_DIR` 或直接改 csproj 配置；
- 反编译查源码：`ilspycmd -t 类名 <Managed目录>/Assembly-CSharp.dll`；
- 项目结构与设计原则见下方目录树与代码注释。

### 发布到 Steam 创意工坊

```bash
./publish.sh    # 编译、运行回归测试、生成 release/CustomRocketInterior-v0.4.0.zip
```

打开 Steam 库 → 工具 → **Oxygen Not Included Mod Uploader**，选择现有的
**Custom Rocket Interior**（ID `3789310279`）→ Edit，将数据目录改为 `release/CustomRocketInterior/` 后更新。
保留原条目的标题、描述、预览图和可见性，不要创建新条目。
`publish.sh` 只准备发布包，不会登录 Steam 或执行上传。

PLib 4.25.0 已合并到模组 DLL 中，不再分发独立 `PLib.dll`，避免加载其他模组的旧副本。
手动覆盖安装时，删除此模组文件夹内旧的 `PLib.dll`，保留 `config.json`。

```
src/
├── ILRepack.targets             # 把 PLib 合并到模组 DLL
├── Mod.cs                        # 入口（读选项 → 套用配置 → 注册设置界面）
├── Strings.cs                    # 设置界面字符串表（英文源文本）+ 翻译加载
├── Options/RocketInteriorOptions.cs  # PLib 设置项（显示名走字符串表）
├── Config/InteriorSizeConfig.cs  # 运行时配置
├── Core/InteriorResizer.cs       # 核心纯逻辑：模板重塑
├── translations/zh.po            # 设置界面的简体中文翻译
└── Patches/
    ├── TemplateCachePatch.cs         # 拦截模板加载并重塑
    ├── RevealInteriorWorldPatch.cs   # 内部世界全图去迷雾
    └── ApplyLiveOptionsPatch.cs      # 建火箭前实时应用最新选项
```

## 已知限制

- 只对新建火箭生效；外景贴图仍是原版大小（视觉问题，不影响玩法）；
- 小地图上的火箭足迹多边形会随新墙体坐标扩大，可能与外景不一致；
- 开启“允许拆除火箭内墙”后，舱壳与窗砖均可正常拆除（含中子质），四个固定气液端口与舱门不可拆。拆墙不会扩大内部世界。

## 许可证

MIT — 详见 [LICENSE](LICENSE)。

## v0.3.0 修复

- 合并 PLib 以避免旧 `PLib.dll` 被抢先加载而触发 `TMPro.FaceInfo` 配置界面崩溃（issue #1）。
- 设置界面随游戏语言切换中英文，沿用原有数字材质配置值。
- 修复同尺寸换材质被跳过，以及缩小缓存模板后残留旧真空格的问题。
- 规范 12–96 的尺寸范围和无效材质配置；修改选项不再清空其他模板的缓存。
- 发布前自动运行模板逻辑测试与合并 DLL 检查。实际 UI/存档验证结果见发布说明。
