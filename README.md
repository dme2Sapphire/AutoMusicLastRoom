# AutoMusicLastRoom

一个 Celeste（蔚蓝）Everest mod：带着一命草莓进入一张图真正的主要最后一面时，自动播放音乐。

最初的想法是到达倒一房间时播放《雨爱 DJ 版》，但曲目有版权，所以仓库**不附带任何音乐文件**。请自行准备 OGG 并放入 mod 的 `Audio/` 目录。

Mod会尝试使用一些规则启发式的推断最后一个GP房间，不过对于有传送(如烈冕号等地图)或者比较非线性的图(如世界大穴等地图)，检测是不准确的。如果你在CCT（[Celeste Consistency Tracker](https://github.com/viddie/ConsistencyTrackerMod)中设置了路径，会有助于启发式推断的检测，但仍不保证100%的准确率。

## 功能

- 检测金草莓、银草莓、铂金草莓；忽略 `SpeedBerry`
- 推断地图的“主要最后一面”，而不是简单取地图文件里的最后一个房间
- 优先使用 [ConsistencyTracker](https://github.com/viddie/ConsistencyTrackerMod) 的炼金路径（可选依赖）
- 没有 CCT 时，根据结算触发器、心之碎片和房间拓扑做启发式推断
- 自动跳过 CollabUtils2 的地图大厅（lobby）和 Gym
- 进入结算/过场房时继续播放，不因切面而停止
- 音乐在进入关卡时预加载，避免到达目标房间时卡顿
- 淡入淡出；死亡后在目标房间复活不会停止/重播

具体推断规则见源码中的 `Source/LastRoomResolver.cs`，开发上下文见 `AGENTS.md`。

## 选项

主菜单：

- 启用自动音乐
- 仅在携带一命草莓时播放
- 音乐选择（自动扫描 `Audio/` 下的 `.ogg` / `.qoa`）
- 音乐音量
- 循环播放

高级：

- 使用 ConsistencyTracker 路径
- 启用小房间启发式排除
- 小房间面积比例：`0.01` / `0.1` / `0.2` / `0.3` / `0.5` / `0.75`，默认 `0.5`
- 离开目标房间时停止
- 记录目标房间判定

隐藏配置：`ExtraOneLifeBerryTypes`、`MusicEvent`（自定义 FMOD bank 事件）。

## 行为说明

- 主要最后一面通常取：
  - CCT 路径中的最后一个 gameplay 房间；
  - 或没有 CCT 时，带结算标记（`eventTrigger`、`LevelExit`、`completeAreaTrigger`、真实心之碎片等）的房间；
  - 如果该房间是过场/结算小房间，则回退到它前面的主玩法房间。
- “小房间”判断为：`最后一面面积 < 前一面面积 × 比例`。
- 结算房间（如吃心面、篝火/事件结算面）不会停止正在播放的音乐。
- Collab lobby / Gym 内不会触发。
- `FakeHeart` / `FakeMiniHeart` 等假心不会被当作结算标记；真实 `MiniHeart`、`CrystalHeart` 会。
- 没有音乐文件时 mod 保持静默，不影响游戏原声。

## 安装

把发布包解压或把 zip 放进：

```text
Celeste/Mods/AutoMusicLastRoom/
```

确保目录结构类似：

```text
AutoMusicLastRoom/
  everest.yaml
  bin/AutoMusicLastRoom.dll
  Dialog/
  Audio/
```

然后在 CeleMod / Everest 中启用。

## 添加音乐

把你自己的 `.ogg` 放到已安装 mod 的：

```text
Mods/AutoMusicLastRoom/Audio/
```

默认音乐路径是：

```text
Audio/rain_love.ogg
```

如果使用其他文件名，在游戏内的 mod 选项里选择即可。

MP3 可以先转成 OGG：

```bash
ffmpeg -i input.mp3 -c:a libvorbis -q:a 6 rain_love.ogg
```

## 构建

需要：

- .NET 8 SDK
- 已安装 Celeste + Everest

```bash
dotnet build Source/AutoMusicLastRoom.csproj -c Release
```

默认读取：

```text
C:\Program Files (x86)\Steam\steamapps\common\Celeste
```

如果不是默认安装路径：

```bash
dotnet build Source/AutoMusicLastRoom.csproj -c Release -p:CelestePrefix="你的 Celeste 路径"
```

输出：

```text
bin/AutoMusicLastRoom.dll
```

将 `everest.yaml`、`bin/`、`Dialog/` 和你的 `Audio/` 放到同一个 mod 目录即可。

## 可选依赖

- ConsistencyTracker 2.x：提供炼金路径
- CollabUtils2 1.x：提供 lobby / gym 识别

两者都是可选的；缺失时会使用内置启发式或标记检测。

## 调试

游戏内控制台（默认 `~`）可用：

```text
automusic_state
automusic_target
automusic_levels
automusic_room
automusic_test
automusic_stop
```

## 版权

本仓库不包含任何歌曲音频。请只使用你有权使用的音乐文件。
