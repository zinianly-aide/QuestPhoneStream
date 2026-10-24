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
