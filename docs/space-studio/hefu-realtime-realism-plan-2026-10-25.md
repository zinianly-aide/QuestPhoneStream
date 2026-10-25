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
