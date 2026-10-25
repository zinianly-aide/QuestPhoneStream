# HefuVR｜写实布景、窗景与实时光照制作方案（2026-10-25）

> **状态：P0 已落地在本地 HefuVR-Android Unity 工程，自动化场景 QA 通过；实机视觉/性能尚未验收。P1–P3 是后续实施计划，不应表述为已实现。**
>
> 目标参考：用户确认的《和府 HefuVR 全屋布景与动线设计》图。参考图是概念效果图，不能替代真实 CAD、工程照片或实测的窗外景观。

## 1. 唯一有效的几何与朝向

- 当前户型：**北侧入户**，依次进入餐厅、客厅、南向阳台；厨房在东北，厨房门必须是玻璃门；主卧东南，西侧其他卧室/卫浴。
- 原 Unity CAD 转换关系：`X=-CAD X`、`Z=-CAD Y`；正 Z 为北，负 Z 为南。不能从单幅透视图推断错位的北门、厨房门、冰箱方向。
- 厨房实际生成逻辑：**北墙燃气灶和油烟机；东墙水槽与热水器**。冰箱在北侧餐边柜附近，原有 `Move_Fridge` 位置及移动状态不改。
- 南阳台 CAD 窗玻璃的原始中心约 `Z=-9.86~-10.02m`。户外植被仅放于 `Z<-13m`；远景建筑位于 `Z≈-40m`。
- 已存在 17 个 `HefuMovable` 可移动根节点，沙发/茶几/餐桌/冰箱位置、HefuLayoutPersistence、WebRTC、生日布景与开门交互必须保持兼容。
- 房屋高度 2.7 m 是当前近似标称；各门、窗、窗台、家具缝隙及庭景距离没有全部现场测量，均需与实物核验。

## 2. 视觉目标（由参考图拆解）

| 部分 | 目标可见效果 | 技术手段 | 状态 |
|---|---|---|---|
| 地面 | 灰米色大理石/瓷砖，轻微纹理及接缝，柔和反射 | 0.78 m 世界尺度地面 UV、颜色/法线图、Standard Smoothness | P0 已有预览，尺寸待核 |
| 餐桌/茶几 | 胡桃/橡木顺纹、倒角、边缘高光、抽屉五金 | 材质贴图已接入；Blender 加倒角/UV/五金进一步提升 | 纹理已接入；模型 P1 |
| 墨绿皮沙发 | 不过分发亮的皮革、座包褶皱、走线与软组织体积 | 皮革颜色/法线已接入；Blender 多层 cushions/bevel/normal baking | 材质已有；形态 P1 |
| 门窗玻璃 | 透过门看见厨房，透过南窗看见室外；边缘细框 | Standard Transparent/Fade、金属边框；后续 Fresnel/探针 | P0 已实现透视近似 |
| 采光 | 从南侧进入的日光、暖室内氛围、地面与家具软阴影 | 仅 1 盏方向光投阴影；暖环境光；之后烘焙 GI | P0 方向已对齐，GI P2 |
| 室外 | 蓝灰天空、远处低对比楼体、前中景绿树层次 | 2:1 全景天空盒、Blender 低面树、低面建筑盒、线性雾 | P0 插画式占位 |
| 生日布景 | 照片框和气球围绕沙发背墙，灯效克制、通路不堵 | 保留原 `HefuBirthdayScene` 与布局切换 | 保留，P2 视觉升级 |

## 3. Unity P0：这次实际落地的代码与资产

**工程**：`/Volumes/ssd/HefuVR-Android`（独立 Unity 2022.3.62f3c1 工程，目前不是该 Git 仓库的一部分）。

- `Assets/Hefu/Editor/HefuSceneAtmosphereBuilder.cs`：进入 `HefuAndroidBuilder.Prepare` 末尾、生日对象临时 QA 之前执行。
- `Assets/Hefu/Editor/HefuSceneAtmosphereQA.cs`：检测厨房可点击门、滑门、18 组 CAD 玻璃、树/楼体是否位于南边、零户外碰撞体、天空盒和太阳方向以及 17 个可移动对象。
- `Assets/Hefu/Textures/OutdoorPreview/Illustrative_DaylightSky.png`：1280×640 双向 equirect 全景，是**程序化天空**，不是实景照片/HDRI。
- `Assets/Hefu/Models/OutdoorPreview/OutdoorOliveTree_A/B/C.fbx`：Blender 4.5.3 生成，5 个复用实例，约 33KB/源文件。无碰撞体与投射阴影，避免增加物理/阴影复杂度。
- 室外 `Hefu_OutdoorPlaceholder_v1`：5 树 + 9 楼体，均位于南窗外，远景使用灰蓝色雾化配色。楼体是抽象占位，不表示某个城市。
- 原 CAD `*WindowCell*_glass` 使用一份 `HefuClearArchitecturalGlass` 材质。厨房 `Door_Kitchen/DoorHinge/DoorLeaf` 的可见板换成可透视材质，并加深色细框；保留原铰链、碰撞与 `HefuEntryDoor`。阳台书房双滑门也替换玻璃材质，保留 `HefuSlidingDoor`。
- 日光 `BakedLike_Sun_VisualPreview` 从南方（负 Z）照向北方（正 Z），暖白色；仅它投射实时柔和阴影，距离 11 m / 中档阴影图；天空环境光与线性远景雾辅助层次感。
- 地面/木纹/皮革的 v3.2.7 贴图（含法线）仍单独保留在 `assets/public/hefu-cad/textures/photo-pbr/`；本轮不改变 `HefuPhotoMaterialFinish` 或保存协议。

**重要限制**：这里的玻璃是 Alpha 透视近似，不具备实体玻璃折射、厚度吸收及正确菲涅尔反射；手机暂未烘焙全局光照，也没有照片级外景地理复现。

### 如何从仓库复现

- 已公开的本轮 Blender/Unity 源文件位于 `assets/public/hefu-cad/atmosphere/`。
- 重生成树：Blender 4.5.3 `-b --python scripts/hefu/generate_trees_blender.py`；重生成天空：`python3 scripts/hefu/generate_illustrative_sky.py`。当前脚本中生成目的路径仍为本地 HefuVR-Android，迁移 CI 前需把输出目录改为参数。
- Unity Builder 注入与断言调用参见 `atmosphere/unity-editor/`；运行 `HefuVR.Editor.HefuAndroidBuilder.Prepare`，再运行 `HefuVR.Editor.HefuSceneAtmosphereQA.Validate`。
- 本地验证成功标记：`HEFU_ATMOSPHERE_GLASS_PASS`、`HEFU_ATMOSPHERE_OUTDOOR_PASS`、`HEFU_ATMOSPHERE_LIGHT_PASS`、`HEFU_ATMOSPHERE_QA_PASS`。无设备在线不声称真机成功。

## 4. Blender 需要具体完成的工作（P1）

**必须进入 Blender 的是“形状”，不是通用换色。**

1. **沙发**：分别建模三个独立座包、靠背包、两侧枕形扶手；采用 Subdivision 1–2 / bevel ~1–3 cm、Weighted Normal；增加软包局部压陷、走线与轮廓层次。保持外部尺寸和 `Move_Sofa` 根坐标不变，导出不带碰撞的 FBX/GLB；中低面手机优先（建议整体 ≤25k triangles），Quest 可另用 LOD0/1。
2. **胡桃木茶几**：让桌边厚度、抽屉缝、拉手与桌腿连接真实；斜切/微倒角 2–4 mm、硬边与加权法线，手柄独立 mesh；木纹按板件方向拆 UV。保持 `Move_Coffee_Table` 和原 Collider。
3. **餐桌/餐椅**：桌面边缘倒角、腿部连接，椅面海绵与靠背包边、实木框与布套独立物体；不通过一个大 Box 假装整个家具。
4. **玻璃与窗框**：先精确测得厨房玻璃门的横竖框、窗框厚度、把手、密封条；Blender 只做框体几何，Unity 负责透明材质和设备端渲染。玻璃透视不能使用带固定光影的整张参考图。
5. **室外绿植**：当前只有示意树 FBX；下一阶段可使用 3D leaf-card/贴图枝叶、法线方向、LODGroup、Alpha Clipping 和风摆动（默认关闭）。资产应具有明确使用授权；不用未经授权的付费植物库。

**材质交付规格**：每个主要物体优先 1024×1024 `BaseColor (sRGB)` + `Normal (linear)` + `Metallic/Smoothness`（线性打包）+ 必要时 `AO`；小五金 512 即可，共享图集；UV 间隔 ≥4 px (1K)，保证平铺方向稳定。模型制作统一 1 Unity unit = 1 m，门洞、窗口以 CAD 尺寸为约束。

## 5. Unity 进阶写实路线（P2）

1. **光照**：室外太阳 1 shadowed directional；先做室内固定几何 Static 标记，烘焙柔和间接光（Baked GI / light probes），但**移动家具不能错误绑定静态光照图**。每个房间设置不同照度/色温前必须有可重复的相机曝光设置。
2. **反射**：优先局部 Baked Reflection Probe（客厅、餐厨、窗外各 1 个），将玻璃反射与地面高光控制到接近实际；动态实时探针/SRR 不作为手机默认方案。URP 迁移另开分支测性能，不为“逼真”直接改 HDRP。
3. **玻璃材质**：在同一 Built-in 管线先用预设透明玻璃（当前已执行）；之后考虑视角 Fresnel（低成本 Shader）、窗框高光与内外 tint；解决移动端 Alpha 排序问题，再决定是否做厚度吸收。室外树木不应穿过玻璃或门洞。
4. **室外背景**：从现有示意 skyline 升级为有许可的 HDRI、树木贴图/法线、低多边形远建筑；雾与对比度匹配远近距离。没有实景拍摄时只能称为“可替换占位”。
5. **后期**：渐进加入轻微 Tonemapping、Color Grading 和低成本抗锯齿；Bloom/SSAO 可提供开关，不默认强开。移动端避免同时开启多层透明、每像素灯、SSR、体积雾和多阴影。

## 6. 场景管理与动线（P2）

- `Daily`: 常态餐厨/客厅、无气球、柔和室内灯；`Birthday`: 在相同 CAD/家具布局上叠加照片墙、气球与摆件；两种模式必须复用家具布局持久化 ID。
- 入户→餐厅→客厅→阳台主路线尽量保持 **≥0.9m 参考净宽**；餐椅后侧活动空间建议 0.8–0.9m，沙发与茶几之间建议 0.35–0.45m（是设计建议，不是从 CAD 已验证的净宽）。
- 厨房玻璃门转动/餐椅拉出/冰箱开门应进行碰撞包围盒测试，避免生日气球和装饰封堵人流。
- 空间标注/保存功能：照片视角与 Unity 正交/透视视角需共享真实的北向元数据；不要将 AI 效果图用作尺寸基准。

## 7. 性能、质量和交付验收（P3）

| 目标环境 | 起始预算/验收建议 |
|---|---|
| 安卓手机（开发/发布） | 目标 30 FPS 稳定（高端设备可试 60），移动/转头/拖家具过程中记录 P50/P95 帧时，不仅看平均 FPS |
| Quest 3S | 另行验证立体渲染及 XR 帧率预算；不可用手机 APK 的 30 FPS 结果替代 72/90 Hz 实测 |
| 光影 | 初版 ≤1 shadowed directional，阴影距离 11m；每像素光不要无上限增加 |
| 3D/贴图 | 室外 5 个树实例/9 个抽象远景楼体；花费/三角面数需由 Build Report/Frame Debugger 实测，而非声称极低开销 |
| 真机清单 | 厨房门打开关闭/玻璃可透、南窗外可见树和天空、门窗无遮挡、室内外明暗过渡、场景切换、17 家具移动/重启保存、生日装饰切换 |
| 安全回滚 | 保留 v3.2.7 包、原始 CAD 与生成前脚本备份；更新手机 APK 用原签名覆盖安装，**不清空数据** |

### 必须补拍/测量（决定照片级效果的因素）

- 面向南阳台的窗台高度、窗高/窗宽以及手机真实观察视线高度。
- 早中晚同一机位照片用于阳光方位及颜色对齐；需要实际城市窗景时必须用户明确授权提供真实窗外照片。
- 沙发的**真实尺寸**、实际型号/面料样板照片，茶几木纹大样、地砖尺寸/纹理及玻璃门实拍。
- 设备 GPU、当前 3D 模式的真实 P50/P95 FPS、温度、占用和视觉截图。

## 8. 代码与 Git 协作边界

- 主仓库 `feat/scene-workspace` 仍是部署/设计资料入口；本机源工程 `/Volumes/ssd/HefuVR-Android` 独立于该 Git 仓库。通过 `assets/public/hefu-cad/atmosphere/` 公布可复现源码/资产，不包含个人场景记录。
- `AGENTS.md` 与既有 XR/OpenXR 修改不能被“优化画质”任务复位或纳入提交；只提交本轮的文档和显式白名单源码。
- 不合并 `main`；不更改 QuestPhoneStream signaling、WebRTC 和 Android control 协议。Quest 端独立实施前需检查它的渲染后端对天空盒、透明材质、AssetBundle 的支持。

## 9. 2026-10-25 P1/P2 第一轮执行记录（v3.2.9）

**已落地**：Blender 4.5.3 中生成沙发和双抽茶几的可编辑 `.blend` 及 2 个 FBX，包含软包几何、局部圆角、扶手/抱枕、茶几抽屉拉手；作为原 `Move_Sofa`、`Move_Coffee_Table` 的 visual-only 子层安装，并隐藏旧 B 类程序化网格。新模型复用原 PBR 纹理材质；原 FBX CAD 不改、17 个移动 ID/碰撞不变。

**P2 局部落地**：餐桌和客厅各增加 1 盏非投影暖色点光源，低强度、有限范围；手机使用 skybox 环境反射，**尚未执行室内光照烘焙、局部 ReflectionProbe 捕获或跨平台 HDRP**。新增光源固定在室内，移动桌子/沙发不会使灯具跟着移动。

**测试事实**：`HefuP1P2RegressionSuite.Validate` 复用六组 Editor QA，成功验证 Blender 模型 bounds/UV/material、旧视觉隐藏、厨房/窗户、光照、17 件家具与 9 个预置视角。俯视 `top_n` 的效果不应替代 `living`/`balcony`/`kitchen` 人眼视角验收。设备当前不在线，不能声称实机 FPS 达标。

**下一轮 P2 待办**：静态 CAD 室内（墙面/固定柜体）的间接光 bake / light probes（不能将可移动家具当 Static），少量低成本 ReflectionProbe 的烘焙和手机实测；再执行现有帧时、发热、玻璃透明排序回归。优先根据新手机实机截图调整材质和曝光，不添加无益摆件。

[可编辑 Blender、Unity 与复现说明](../../assets/public/hefu-cad/interior-p1p2/README.md)

## 10. 2026-10-25 P3 真实 Unity 对比整改（v3.3.0）

用户提供手机 `corridor` / `top_n` 画面，暴露沙发“没有座”、南窗一片灰蓝两个关键问题；不能用原先的几何存在性 QA 假定画面可见。

- **沙发**：Metal Editor 从电视位置渲染发现 TV 面向的是整块沙发背部；将 FBX 子层转向 180°，不旋转 `Move_Sofa` 根。新渲染证明 3 个座包可见。新 `HEFU_SOFA_SEAT_FRONT_QA_PASS` 针对导入朝向增加防回归断言。
- **南窗**：CAD 的 21/22/23、27/28/29 每组为三片玻璃，旧材质叠加导致偏蓝不透明。保持 18 片窗玻璃对象，南窗仅中心 22/28 参与绘制并用 alpha=.075 的 Fade 材质；无需砍掉原窗框或对改变 CAD。
- **窗外树**：原 Blender FBX root 的 100 倍导入缩放和轴系旋转不应覆盖。修复后五棵树站立，有 2.3–8m 高度断言。窗外仍是低多边形占位，不等于参考图中真实光感或现实城市景观。
- **真渲染**：Unity `-batchmode -quit`（**不用** `-nographics`）通过 GPU 摄像机生成 SofaFront、SouthWindow、Living 800×560 PNG 和沙发修复前后对比；记录于 `docs/space-studio/qa/v3.3.0/README.md`，供后续做相同机位对比。

### P4 优先路线：朝视觉接近概念效果图前进

1. 整体建筑真实材质应补墙面哑光细节、吊顶与窗帘布料、电视壁柜倒角，而不是继续以大白块替代；固定构件与可移动家具严格分离。
2. 提升室外植被为具授权 foliage alpha-card/法线贴图和 LODs，并针对南侧实际遮挡/视野距离布置，保证手机平滑滚动。
3. 用静态墙地面单独 bake 间接照明、LightProbes 覆盖可移动家具，客厅/餐厨局部**烘焙** ReflectionProbe；禁止动态实时 probe 捕获拖慢手机。
4. Unity 固定 `sofa_front`、`south_window`、`living`，以及真机 `corridor`、`top_n` 的画面验收都需提供具体截图和材质亮度/可见性对比。目标“八成接近效果图”是主观体验目标，不是可由 Editor QA 单独给出通过百分比的客观指标。
5. 手机上的 3D 探索 FPS、发热、17 个家具移动/重启保存、相机转向和玻璃排序必须完整验收。

## 11. 2026-10-25 洛阳深秋太阳/室外场景阶段（v3.3.1）

实际计算来源：洛阳约 34.6836°N/112.4536°E，UTC+8；2026-10-25 天文数据约日出 06:44、日落 17:44、正午 12:14/高度 43.3°。使用 NOAA 简化天文公式而不是固定 Unity 光照方向，在固定 **14:30** 的效果预设下，太阳高度约 33.4°、方位 220.9°（正北顺时针，西南）；只有原有 Directional Light 投射实时阴影。

已落地：偏暖秋日天空贴图、微黄雾色和较低日光、五棵树的绿/黄/褐树冠配色、干草与土壤纹理的单网格户外地面、远景建筑秋季对比度调整。保留户型北入户、南阳台、17 家具、沙发座面、透明厨房门/南窗。**全部室外景观仍是可替换的模拟占位，不能代表真实洛阳街景。**

已执行：`HefuLuoyangAutumnRegression.Validate` = 太阳/秋季专项 QA + 原有 6 组 Unity Regression PASS；Unity GPU 使用和 v3.3.0 完全对应的 `south_window`/`living`/`sofa_front` 固定机位输出 800×560 PNG，再生成左右对比图，证据见 `docs/space-studio/qa/v3.3.1/`。

未完成：动态时刻与季节切换 UI、真实天气接入、墙体烘焙 GI、家具 Light Probes、真实室外树木叶片贴图与 LOD、局部 Reflection Probe、安卓实机透明排序/帧时。下一优先任务**先改善叶片几何 + 测量窗台和楼层高度，再在固定建筑与移动家具分离基础上实施静态光照 bake**，而不是继续叠加未经验证的全场后处理。所谓 80% 效果图是主观视觉目标，不能据此声称已经达到。

## 12. 2026-10-25 暖奶油 / 胡桃木 / 墨绿风格局部落地（v3.3.2）

遵循用户给出的《和府 HefuVR》参考风格：不把 AI 效果图当 CAD，保留北入户、东北厨房玻璃门、南窗、冰箱与 17 件原家具的空间关系。实际落地的 P4 轻量软装层 `Hefu_WarmReferenceStyle_v1`：在现有餐桌中心上方布置 2 盏曲面象牙白吊灯 + 黄铜吊杆/顶盘（不加新的实时点光）；南向客厅窗框两侧增加 2 块带程序化亚麻布纹的褶皱窄布帘和细帘杆，不挡中间窗玻璃，不添加碰撞与控制协议；现有 `Style` 的 Neutral/Warm wood 墙面基色改为暖白/暖奶油，Cool gray 和模式切换逻辑不变。

`HefuWarmReferenceStyleQA` 与 `HefuWarmReferenceRegression` 用于确认双吊灯/双布帘位置、材质及几何、17 家具、Style 切换、原六项太阳/玻璃/动线回归。Unity GPU 固定机位对比与脚本存档于 `docs/space-studio/qa/v3.3.2/`、`assets/public/hefu-cad/interior-warm-style/`。

**明确未交付**：物理全局照明和间接反射烘焙、吊灯真实照度分布、制造商家具材质和贴图扫描、室外写实 foliage alpha-card/LOD、设备上主观 80% 视觉验收。下一阶段优先**固定建筑受光与移动家具分离的光照烘焙/Probe 验证**，再按设备性能精修树木纹理；新软装仅是明确可回退的一小步。

## 13. 2026-10-25 白模感根因修复 — 墙面 UV / 踢脚线 / 烘焙反射（v3.3.3）

实际调查发现 Unity **20 段 CAD 墙体的 Mesh.uv 全为 0 长度**，是大量墙面只能显示纯色、无法正确铺贴墙漆颗粒与法线的直接根因。保留真实 CAD 墙体位置、MeshCollider 与现有 Style 三档切换，只在生成 Mobile 场景的 MeshFilter 上分配独立 UV 网格资产（1m 世界尺度平铺），使用程序化哑光奶油乳胶漆 BaseColor/NormalMap，金属度 0、平滑度 .18。

沿实际 CAD 墙面贴地的三角面边生成 **107 段踢脚线**，合成单个 mesh 与材质，避免在房门处手工猜测一整条饰条，没有额外碰撞体或灯光。另外成功在 Mac Unity Editor 进行两次反射探针烘焙，生成客厅/餐厅各一个 64px Cubemap（`Custom` box reflection，手机不实时捕获），确认为真实 3D 场景截取、不是假 HDRI 或静态 GI。

自动检查 `HefuWallPBRRegression.Validate` 通过，且复用既有暖色装饰、洛阳季节、厨房/玻璃、沙发与 17 家具的联动测试。960×640 Mac Unity 真实机位渲染及同机位前后对比见 `docs/space-studio/qa/v3.3.3/`；可编辑源码/资源见 `assets/public/hefu-cad/wall-pbr-probes/`。

**未实施与下一阶段**：固定建筑正式烘焙 GI/lightmaps、移动家具 Light Probes、顶面/吊顶在俯视与室内视角的显示策略、真实橱柜圆角/木纹及树木 LOD。当前仍能看出低模感，不能用验证通过冒充“效果图 80%”。下一阶段应优先补齐屋顶/室内 GI 的建模条件和明确的 camera mode 开关，再单独测试手机帧时。

## 14. 2026-10-25 P5 CAD 顶面 + 真实静态 GI（v3.3.4）

从原始真实户型的 `00_客餐厅/过道` 地面向上反向投影 16 个三角面，构建 y=2.677m 朝下的顶面及 18 段同网格檐线。相机眼高低于 2.47m 且位于客餐厅内部时显示顶面；俯视/房外自动隐藏，不改移动根/碰撞/布局存储。

经过多轮实际 Unity 烘焙实验，最终为 18 段不重叠的固定 CAD 墙面（另两段 35/42 原始重合墙保留渲染但不参与 GI）、原地面和顶面启用 ContributeGI（共 20），生成 UV2 贴图并烘焙一张 lightmap 与 46 个 Light Probe；17 件家具保持可移动并启用 BlendProbes。初次出现 11 项 Lightmap UV 重叠，优化 padding 和排除重复 CAD 静态标签后无警告。GPU 使用 Mac M4 的 Progressive Lightmapper。

注意在单进程内直接连续执行 `Prepare→GPU Bake→Android Build` 会发生 Unity `RadeonRaysJobCompositeLightmap` 原生段错误，不能把这种路径发布为正常流程；必须分独立 Unity 进程：Prepare / Bake / QA / ExportPrepared。具体复现、实际光照贴图、固定机位图和性能风险见 `assets/public/hefu-cad/ceiling-gi/README.md` 与 `docs/space-studio/qa/v3.3.4/`。

**未完成**：所有房间独立吊顶、室内摄影级木纹柜体倒角、户外高精度树木、手机 GPU 帧时与动态物理遮挡；Lightmap 是固定 14:30 秋季环境，切换顶面显示不重新计算间接光。即便静态烘焙通过，也不等于达到参考效果图八成，需要实际手机场景截图再验收。

## 15. 2026-10-25 v3.3.5 室内曝光修复 + 真实尺度柜门木纹

以 v3.3.4 同机位 Unity Metal 960×640 实际图像偏暗为根因：两盏旧无影暖色补光强度 .38/.27，加入 2.677m 顶面与 GI 之后室内曝光不足；保留既有光源数量/方向与移动端 pixelLightCount=3，将两盏补光提高至 1.15/.92（有效范围分别 5.2m/5.4m），提高环境漫反射(.84/.814/.77)，原秋季太阳方向不变、强度调整为 1.04，重新独立 Unity 进程 GI 烘焙。

直接对比图像统计（整个画面 FFmpeg YAVG）：餐厅 91.32→127.50（+39.6%）、客厅 89.13→121.64（+36.5%）、南窗 74.10→99.26（+34.0%）。不是场景真实照度单位，也不能声称已达到效果图八成。

保留供应商厨房/餐边柜尺寸和原门板/碰撞体，在餐边柜及五个已放置柜体根节点下，共对 **60 块门板生成约 3mm 倒角前面**并整合到 6 个网格中；针对厨房东墙水槽立面使用 -X 朝向，不会误放到北墙；餐边柜胡桃木开放格/台面 **8 个表面**按 0.48m 真尺度 UV 投影并使用原本已经授权/本地的木纹及 normal 图，奶油柜门贴图程序化生成可复现；不使用整幅效果图贴图。`HefuExposureCabinetQA` + `HefuRoofGiQA` + 老 `HefuWallPBRRegression` 全部通过，旧 QA 灯光范围从 4.6m 调整为 5.6m，仍检查无新增实时阴影灯。源码/真实 GPU 对比证据见 `assets/public/hefu-cad/daylight-cabinet-pbr/` 与 `docs/space-studio/qa/v3.3.5/`。

后续优先：手机实机曝光/帧时间、厨卫柜体实体边缘圆角与五金、真实反射和窗外树叶 alpha-card 材质，夜间照明模式与昼夜调节需要独立设计而不是暗室靠拉高 Gamma 修复。Mac 端系统数据盘仍约剩 3.4GiB，原始 CAD 和 XR 未修改。

## 17. 2026-10-25 v3.3.6 三盏有形灯具 + 日间/生日双重烘焙

用户提出的 6 项均落地：复用餐桌两盏奶油色黄铜吊灯，客厅新建落地灯；灯罩和灯芯材质开启 `_EMISSION`；两盏向下 72° Spotlight 真正安装在吊灯下缘，落地灯的 PointLight 位于实体灯罩内，删除原 2 个悬空补光点。阳光仍是唯一带实时阴影的主要光源。

通过独立 Mac Unity GPU 进程，分别烘焙 Day 和 Birthday 两套真实 128×128 Lightmap + 每套 46 个 SH LightProbe；手机使用现有 Birthday ON / Day ambience / Night ambience 控件原子切换烘焙纹理、探针、灯罩 Emission、灯光强度及太阳和环境亮度，不需要在手机上实时烘焙。

光源遮挡复查发现落地灯首次被原始 `12_CAD_wall_12` 挡住；随后移至沙发南端靠客厅/电视一侧并新增 `Physics.Raycast` 自动验收。两模式三机位（餐桌、客厅、落地灯）共 6 张 Unity 真实 GPU 截图和 3 张同机位左右对比图存放在 `docs/space-studio/qa/v3.3.6`。源码与双份光照数据见 `assets/public/hefu-cad/visible-lights-dual-gi`。

保持 17 件家具、玻璃、厨房、North Entry、Room Ceiling 和原 XR/控制协议不变。后续重点：安卓真机照度、卡顿/发热、Birthday 切换后 Lightmap/Probe 是否及时更新、剩余柜体细节，以及灯具专业模型。不能因 Editor 测试通过而宣称手机 FPS 或照片级 80% 相似度。

## 18. 2026-10-25 v3.3.7 — 实体光源亮度舒适度 / 更细静态 GI / 低开销模式切换

延续 3.3.6 实体灯具：不增加灯、不改厨房和 CAD，不通过整体滤镜泛白补光；仅提高真实双吊灯下射 Spot / 落地灯内 Point 的强度与覆盖（79° / 4.05m），白天太阳强度微调至 1.13，生日保持局部暖光。Unity Metal 同机位平均 YAVG：Day 餐厅 **105.37→120.12**、Day 客厅 **102.89→133.57**、Birthday 餐厅 **69.78→81.29**。光源均来自场景实体灯，非真实 lux 数值。

光照贴图精度 7→12 texel/m，Direct/Indirect 采样 32/96→48/128，结果两套静态纹理由 **128²→256²**，46 个探针每套不变，未发现 UV 重叠警告。开关模式时一次展开两个 SH 数组和 LightmapData，缓存灯罩材质，跳过同模式重复应用且不反复 Tetrahedralize；Unity 测试连续切换 16 次通过。

对比图和实际源码见 `docs/space-studio/qa/v3.3.7` 与 `assets/public/hefu-cad/light-comfort-v337`。下一阶段继续对照参考效果图改进柜体细节、材质与真实窗外曝光；仍需真机测量手机 GPU 帧率、夜间灯光过渡与温升；系统数据盘仅余约 3GB。

## 19. 2026-10-25 M2：PBR 细节材质 + 日常软装独立开关

基于真实 Unity 2022 Built-in（非 URP）完成 M2 小步提交。保留 0.78m 地砖实际 UV、48cm 木纹、原始 Blender 沙发及 3mm 柜门倒角。统一地砖 Gloss .34 / Normal .22、胡桃木 Gloss .29 / Normal .21、沙发皮革 Gloss .32 / Normal .30、象牙白门板 Gloss .28、拉丝黄铜 Metal .76 / Gloss .55，窗户使用仍可透视的 Standard 透明材质且不写深度；未增加昂贵的动态后处理。

新增可独立开关 Daily decor：餐边柜真实 Worktop 锚定的小型陶瓷绿植，沙发可移动根下的折叠亚麻毯，材质与几何统一压成两个视觉 MeshRenderer；无新增碰撞、灯光、家具 ID、真实户型位移。手机新增 `Daily decor ON/OFF` 控件，使用独立 PlayerPrefs，不能影响原 Birthday ON/OFF、暖灯和 17 个家具布局存档。

M2 的 Day / Birthday 仍分别用 Unity GPU 烘焙 256px GI 和 46 LightProbes，M2专项 QA、CAD/PBR/玻璃/太阳/沙发回归与 16 次灯光切换通过。五组 M1→M2 同机位 960×640 Metal 实际画面亮度变化均 <1 灰度值，是有意避免靠曝光漂白掩盖材质不足；更清晰的日常软装 ON/OFF 对比已单独输出。

源码/生成资源 `assets/public/hefu-cad/render-upgrade-m2`，照片 `docs/space-studio/qa/m2-material-soft-decor`。仍无法主张达到参考图 80% 写实度，M3 需针对近景真实反射 / 材质细节 / 低中高质量档以及**真实 Android/Quest FPS**推进。不要用 macOS Editor 性能替代真机数值。

## 20. 2026-10-25 M3：真实分房间反射与低/中/高三档画质

反射根因：旧两个 64px EXR 哈希完全相同，Unity `Lightmapping.BakeReflectionProbe()` 实验输出的两个新 128px EXR 也完全相同，不能把仅升级像素作为“局部反射”成果。改为编辑器真实 `Camera.RenderToCubemap` 离线六面场景捕获，得到真正不同的客厅/餐厅 128px RGBA32 贴图；自动测试各面采样 3149 个明显不同的颜色样本。两枚 `ReflectionProbe` 仍然是 Custom + BoxProjection，手机无实时六面抓取或额外渲染相机。

安卓 viewer 新增低 Battery / 中 Balanced（默认）/ 高 Quality 三档。AA Off/2x/4x，阴影 off/9m/13m，pixelLightCount 2/3/4，局部离线反射 on/off 与 1/0 mipmapLimit，目标帧率 45/60/72；增加可选实际运行帧间隔 FPS HUD，独立保存设置，不覆盖 Birthday、M2 Daily decor 或 17 件家具布局。Unity QA 覆盖档位切换、反射内容差异、偏好不被测试改写；CAD/PBR/玻璃/沙发、Day/Birthday 256px GI 和 46 探针、16 次光照切换及屋顶 2.677m 截面回归通过。

保留相同主展示机位的 M2→M3 五组 Unity 对比，三档 Hero 画面见 `docs/space-studio/qa/m3-quality-reflections`；源码 `assets/public/hefu-cad/render-upgrade-m3`。目前默认静态画面整体亮度与 M2 基本相同（预期，因为 M3 不改曝光）；M3 核心收益是**可选 GPU 开销与可验证的房间真实反射**。没有 ADB 真机时不得声称达到某 FPS 或参考图 80% 写实。下一阶段优先接 Android/Quest 真机、帧时与热性能验收和更高细节/导出设置。

## 21. 2026-10-25 M4：五机位 2K 展示导出 + 三档真实设备基准测试入口

沿用 M0 的五套实际 1.55m 高机位，M4 App 新增 `Export 5 views`：使用独立的 **2048×1365** 离屏相机逐张抓取纯 Unity 画面（无 GUI），照片保存到应用 `Exhibits/M4_<UTC>`，并尽力通过 Android MediaStore 保存至系统图库 `Pictures/HefuVR`；附 `manifest.csv`，包含精确机位、朝向、FOV、尺寸、画质档、UTC 时间。抓图后恢复原相机观察位置和 CAD 顶面遮挡设置；不写家具存档、不改 17 件家具和其他用户选项。

另新增 `Bench 3 tiers`：分别 1s 预热/6s 采样 Battery、Balanced、Quality 档位，采集真机帧间隔 CPU/render-loop mean FPS / p50、p95、p99、worst frame ms、Mono/总分配内存、设备/GPU 名称，自动保存 `M4Benchmarks/*.csv`。`finally` 恢复用户选中的画质档，不写偏好。该测试只有**在手机启动并主动点击时**才有真实性能数据，不等于 Android GPU Profiler 或温控/功耗测量。无设备在线不得填入任何指标。

使用 Mac Unity 2022.3 Metal 编辑器在**相同五机位**已真实生成五张 2048×1365 无 GUI 渲染图及 `manifest.csv`，再通过原生 Swift AppKit 拼成一张 **3200×1700 实景展示拼版**（非 AI 效果图），证据 `docs/space-studio/qa/m4-presentation-bench/`；可复现源码与海报脚本 `assets/public/hefu-cad/render-upgrade-m4/`。

完成 GI Day/Birthday 256² + 46 probes 重新烘焙。M4 五机位/CSV 算法 QA、M1 CAD/玻璃/沙发、M2 Daily/Birthday、M3 反射/三档画质、双光照切换及原屋顶 2.677m 全部 Editor PASS。Android Unity Library 输出成功，`versionCode=18/versionName=3.4.1-m4`；宿主 Android 单测 **24/24 PASS**、APK 签名及 ZIP 校验 PASS。**没有 ADB 设备，因此 Gallery 实际手机写入、Bench CSV、真实 FPS/GPU/热衰减、Quest 都未验收**；应以真机记录作为下一步的先决条件。
