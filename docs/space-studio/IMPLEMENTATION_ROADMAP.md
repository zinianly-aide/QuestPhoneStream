# QuestPhoneStream · Space Studio 全量实施路线（2026-10-22）

## 目标与硬性约束

把 QuestPhoneStream 从“空间投屏客户端”演进为“有场景库的通用空间工作台”：

- Home → Spaces → 进入 HefuVR/空白场景/扫描白模/导入模型。
- 在场景里保留 QuestPhoneStream 的 XR Origin、Android/Mac WebRTC 会话、SpatialPanel / Media 面板。
- 可编辑物体：创建、抓取、移动、旋转、缩放、复制、改名、隐藏、删除、恢复、撤销重做、保存与重新打开。
- 室内尺寸一律 **米制 1:1**；HefuVR 已确认平均层高 **2.70m**，不把平均层高误称为各房间实测净高。
- 扫描白模使用 Quest Scene API / Meta XR MR Utility Kit（MRUK），扫描权限和 Space Setup 明确由用户主动授权；不要声称 Quest 3S 能无条件获取摄影测量级完整纹理 CAD。
- 拆分室内结构扫描和单件家具摄影重建：后者复用旧 `origin/feat/object-scan-poc` 的 RGB+位姿采集、Mac worker，不与 MRUK 的房间结构扫描混淆。
- **完整真实住宅模型、照片、空间扫描、APK 均不得推送到 PUBLIC 仓库或公开 Release**；只提交框架/抽象/通用测试，住宅资源以本地私有 AssetBundle 导入。
- **不用切换 Unity 主场景实现 Spaces**：只切换 `SceneWorkspace_EnvironmentRoot`；保持XR、Panel、Signaling、WebRTC和Android远程控制状态。
- 现有 Mac 点击及滚轮分支未提交修改不能覆盖；工作树 `/Volumes/ssd/QuestPhoneStream-SceneWorkspace`，新分支 `feat/scene-workspace` 基于 `79f2c4c`，先不合并。

## 目标架构

```text
QuestPhoneStreamMvp.unity（仅一个 Application/XR Runtime）
├─ QuestPhoneStreamApp（signaling / WebRTC / media / remote control）
├─ XR Origin / Camera Offset / HMD / Controllers（原 QuestXrUiRig）
├─ SpatialPanels（屏幕、视频和工具）
├─ Space Studio（独立新模块）
│  ├─ SceneWorkspaceManager（场景目录和生命周期）
│  ├─ SceneWorkspaceBrowser（世界空间UI）
│  ├─ SceneWorkspace_EnvironmentRoot（唯一允许替换的场景内容）
│  │  ├─ Building（锁定CAD、空白结构、MRUK导出白模）
│  │  └─ ObjectRoot（stable UUID的家具和装饰）
│  ├─ ISceneSource（blank / cad-bundle / mruk-scan）
│  ├─ SceneEditor（XRI射线手柄交互，独立于PhonePanel Input）
│  ├─ SceneInputArbiter（XR UI > Phone Surface > Panel Grab > Furniture > Building）
│  └─ SceneWorkspaceStore（本地JSON + 资源文件 + 原子保存 + 备份）
└─ 未来可选：LAN SceneSync Service（同步，默认关闭）
```

坐标规则：Unity本地世界米制，+Y向上；Hefu原CAD导入转换为Unity +X向东、+Z向北，不能把旧相机朝向当作模型旋转。Spatial Protocol v1 使用右手坐标约定，与Unity本地坐标之间必须经过已经存在的转换器。场景内保存的局部Position/Rotation必须标明所属场景坐标系，跨设备同步时不能直接复制裸Pose。

## 分阶段里程碑与验收

| 关卡 | 可独立交付的工作 | 验收条件 | 依赖/风险 |
| --- | --- | --- | --- |
| **S0.0 代码隔离** | 分支/工作树、私有资源 .gitignore、结构设计、测试脚手架 | 原 `fix/quest-mac-click-control` 状态、WebRTC/协议原样 | 已完成 |
| **S0.1 Scene Kernel** | `SceneWorkspaceRecord`、目录/ID、空白与私有场景入口、场景加载/切换、不影响 XR/WebRTC、软删除、恢复、复制、命名、保存备份、基础UI | Home有 Spaces 入口；切换 Blank 不销毁SpatialPanels；重新打开恢复最后场景；Hefu包缺失显示明确错误且不清空当前场景 | 目前阶段 |
| **S0.2 私有Hefu导入** | HefuVR Quest v0.3.16 资源与脚本拆离为静态 CAD + 16 组家具私有 AssetBundle；场景UID/坐标/2.7m校验；私有安装 | Mac Editor 已真实加载482个网格、绑定16组家具，并通过编辑-切换-恢复烟雾测试；**Quest真机尚未验收** | Unity 2022 Android AssetBundle、房屋资源不能进公开CI |
| **S1.1 Object Editing** | SceneObject stable UUID；通过XRI复用现有手柄射线；移动旋转缩放，吸附/碰撞，编辑锁、场景窗口仲裁 | 16组家具全可选；手机屏幕点击不误拖柜子；建筑墙锁定；没有控制事件泄露到Mac输入 | 手柄ownership、碰撞体代理与原布局矩阵 |
| **S1.2 持久化/编辑历史** | 一步操作一个Command；Undo/Redo、复制/删除/恢复、保存快照、JSON schema migration、scene revision、局部场景事务 | 编辑后退出/重开位置恢复；断电/异常退出仍能从上一有效版恢复；删除可恢复，冲突不静默覆盖 | 房屋结构永不因为删家具而破坏 |
| **S2.0 Scene API/权限门** | Pin 与Unity2022兼容的MRUK/MetaXR包、Scene Data permission、Space Setup引导、未授权fallback、可用性诊断 | 有授权才加载；拒绝后Blank/CAD与WebRTC仍正常；SceneModel不可用明确显示而不是伪造数据 | MRUK与现有OpenXR/XRI的SDK兼容性必须先单独CI编译确认 |
| **S2.1 房间扫描白模** | MRUKRoom/Anchor planes/volumes → 用户可编辑的本地结构记录；floor/wall/ceiling/door/opening、volume 标签；多房间和数据来源 | Quest 3S实际Space Setup后能生成一间真实尺寸房间的门/墙/地面白模并保存再载入 | 不等同彩色高精网格；多房间拼接和浮点漂移 |
| **S2.2 锚点重定位** | 设备Scene anchor到Scene-local Pose绑定；追踪丢失后重新定位；版本变化与重扫匹配、差异审核 | 退出/重启后白模对齐已扫描房间，偏移必须可检测/手动重校 | 系统锚点权限、世界锁定失效 |
| **S3.0 模型导入/导出** | GLB模型、Hefu图纸/资源导入预览、导出JSON+资产manifest；非可信模型白名单和尺寸限制 | 不可信场景/损坏模型不能覆盖有效空间；导入旋转/单位正确 | GLB纹理和运行时导入依赖，需要单独评估 |
| **S3.1 跨端可选同步** | 在用户明确同意后LAN可靠传输版本化布局；冲突和备份；手机/Web共读同一个schema | 手机调整床头柜，Quest/Web显式同步后位置一致；无网络仍可离线编辑 | 以空间会话授权而非dev-token授权共享房屋数据 |
| **S4 完整场景治理** | 场景搜索、缩略图、标签、项目导出、回收站、审计日志、模型优化/LOD | 多个场景快速切换；70/72Hz基准、持续运行不泄漏GPU内存/网络会话 | Quest 3S真实运行性能测试 |

## 代码分层与数据协议

```text
apps/quest-unity-client/Assets/QuestPhoneStream/Scripts/Spaces/
  SceneWorkspaceModels.cs      场景/物体存储模型，独立schema
  SceneWorkspaceStore.cs       私有持久化，catalog.json、<id>.qpsspace和.bak
  SceneWorkspaceManager.cs     资源加载/环境生命周期，保持XR和RTC不变
  SceneWorkspaceBrowser.cs     XR WorldSpace空间目录UI
  [next] SceneObjectManipulator.cs XRI控制器输入/拖动与XRI Surface ownership
  [next] SceneSpaceAlignment.cs   Floor anchor/world-lock/SRP-Unity坐标转换
  [next] MrukSceneSource.cs       Conditional SDK adapter

private Quest persistence:
  Application.persistentDataPath/SceneWorkspaces/catalog.json
  Application.persistentDataPath/SceneWorkspaces/<id>.qpsspace
  Application.persistentDataPath/SceneWorkspaces/Assets/hefu/home.bundle
```

### S0当前范围与明确未实现项

第一步内核允许 blank Studio 的实际空白地面、1m标尺、添加Cube、复制场景、软删非当前场景、Undo、JSON保存/加载，另登记HefuVR私有入口，缺少bundle时不允许误切换。**它尚不是完成的家庭编辑器：无MRUK采集、无外部GLB资产解释器、无跨设备布局同步；HefuVR CAD 私有Android包已在本地生成，并通过 EditorMac 资产加载验证；**头显仍未连接，所以未做设备验收**。**

首个QA要证明：文件路径不可遍历、重复ID/NaN拒绝、schema v1往返、保存.bak回退、切场景仅清空 EnvironmentRoot、原Signal/WebRTC没有文件变更。真实Quest手柄和扫描必须等待设备连接再验收。

## 决策与停损标准

1. S0.1编译、单元测试、Home UI必须过关才能进入S0.2。不要使用公开CI的真实住宅资产进行测试。
2. S0.2资源与主应用拆分：只打包可视几何和可编辑家具索引，移除旧QuestWalk/Camera/AudioListener/网络服务，减少重复XR与MonoBehaviour脚本依赖。
3. 扫描SDK固定兼容版本，不用动态latest；Meta新版MRUK Sample要求Unity 6，旧Unity2022示例/SDK组合需要核实兼容性后再选择升级路径。
4. 锚点/扫描数据未经授权不得上传。用户仅授权房屋结构扫描不意味着授权其RGB照片、云端传输、公司使用或外部共享。
5. 任何S1编辑操作必须在当前场景内，不能覆盖原始HefuCAD；Undo/Redo仅编辑覆写层。
6. 场景切换断开 WebRTC 或回归Mac点击/滚轮能力，则本阶段判FAIL，回滚环境模块而非协议重构。
7. 公开Git repo只能放框架、通用假数据和测试，不能提交真实住宅CAD、全屋GLB/FBX、空间扫描、地图或私有APK。

## Mac开发与验收指令

```bash
cd /Volumes/ssd/QuestPhoneStream-SceneWorkspace
git status -sb
# Unity editor version must match ProjectVersion.txt (2022.3.62f3c1)
/Volumes/ssd/Applications/Unity/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics \
  -projectPath /Volumes/ssd/QuestPhoneStream-SceneWorkspace/apps/quest-unity-client \
  -logFile /tmp/qps-spaces-unity.log
# Unity Test Runner -> PlayMode -> SceneWorkspaceTests
# After tests pass, build ONLY private local APK (not public release)
```

Meta documentation used to plan S2 (not implemented in S0):
- https://developers.meta.com/vr/documentation/unity/unity-mr-utility-kit-manage-scene-data/
- https://developers.meta.com/vr/documentation/unity/unity-mr-utility-kit-gs/
- https://developers.meta.com/vr/documentation/unity/unity-mr-utility-kit-samples/
- https://developers.meta.com/vr/documentation/unity/unity-sample-mruk-basic/

## 2026-10-22 本轮实测结果

- 独立工作树 `feat/scene-workspace` 创建成功；用户当前 `fix/quest-mac-click-control` 的两个未提交文件未被覆盖。
- Unity 2022.3.62f3c1 编辑器编译成功，PlayMode `SceneWorkspaceTests` **5/5 PASS**。
- 私有 Hefu Unity 出口：`/Volumes/ssd/HefuVR-Quest/Builds/SceneWorkspaceBundles/Android/home.bundle` 及 `home-manifest.json`。私有项目保存单独的导出器 `HefuSceneWorkspaceBundleExport.cs`。
- QuestPhoneStream EditorMac 私有集成烟测：真实导入 **482 MeshFilters / 16家具 / 2.7m顶面**，无Hefu MonoBehaviour、Camera、AudioListener；修改一件家具、切换空白、切回Hefu后位置成功恢复。
- QuestPhoneStream Android APK **本地构建通过**，含OpenXR和`Spaces`入口，签名/ZIP完整性检查通过；打包检查没有把私有CAD文件或bundle塞进APK。
- 私有安装器（尚无设备执行）：`/Volumes/ssd/HefuVR-Quest/Scripts/install_private_hefu_into_qps.sh`。设备上的空间场景还需要真机验收。
- 原WebRTC/Signaling/Spatial Protocol v1均没有被重构，也没有merge/push任何分支。

## 2026-10-22 · S1.1 首轮编辑器交互闭环

- 新增 `SceneWorkspaceManipulator`：**手动选择 Spaces > Edit room** 后进入，不重建或修改 XR Origin，不改变WebRTC、Spatial Protocol或远程控制协议。
- 使用原 `QuestXrUiRig` 的 Right Controller 姿态和 `RightHand Grab`（Grip）。Physics.Raycast **只接受最近命中的场景家具**，不穿透位于前方的PhonePanel或墙体，避免 Grab 同时控制房屋和远程屏幕。
- Grip 按住仅预览世界空间移动，Y 保持不变，释放才批量保存为一次 Undo；拖动最大3米；只允许具备 `SceneWorkspaceObject` 标签的物体，CAD结构默认不可编辑。
- 独立 WorldSpace Edit Toolbar：Rotate ±15°、Scale ±5%、Delete、Undo、Done。回到 Spaces Browser 时仍保存原 WebRTC/手机控制 UI 状态。
- 默认进场仍是浏览模式；没有连接真实Quest测试，因此无法宣称控制器与现有SpatialPanel手势完全无冲突。
- 改进 `SceneWorkspaceManager`: 清除旧环境前立即停用旧节点Collider，避免 Deferred Destroy 同帧与Undo/Select冲突；装入新环境后 `Physics.SyncTransforms()`。
- PlayMode 测试 `SceneWorkspaceTests` 从5扩展到 **6/6 PASS**，新增显式编辑、屏幕遮挡优先、移动松开保存、撤销、旋转、缩放、软删除和场景重载闭环。

### S1后续仍未完成

- Quest 3S真机内的 Grip/XRI点击仲裁，尤其切换页面与UI Poke/Ray。
- CAD模型重叠与墙体贴边精度验证、专用碰撞层、家具的复制/恢复、Redo和房间级快照。
- MRUK空间白模及房间扫描权限接入；未引入SDK，不宣称具备扫描能力。
- 性能/人体碰撞/重定位/空间锚点真机验证。


## S2 — Room exploration & stick menu (2026-10-22)

- Home > Spaces > Stick controls: configure each stick independently as
  Off / Move / SnapTurn / SmoothTurn, persisted locally in PlayerPrefs.
- Defaults: left Move (1.2 m/s); right SnapTurn (30 degrees). Speeds 0.6/1.2/1.8
  m/s; 30/45 degree snap angle; smooth turn 45 deg/s.
- Explore room hides the browser without opening Home. Movement is blocked while
  Home, Settings, scene browser, or furniture editing is visible.
- Right-stick Mac wheel takes priority only while aiming at a connected Mac panel.
- Capsule collisions; moving XR Origin, never disabling the physical Quest boundary.
- Spawn at Hefu CAD 00_ floor center; Reset position returns to that reference.
- Headset verification needed for real CAD collision and menu ergonomics.


## S2.1 — Quest height, doors, and visual affordances

- Height root now requests Floor tracking; 1.6m Device fallback is retained only
  where the XR runtime cannot supply a floor-referenced origin.
- Eye trim moves the shared camera/controller calibration root ±20cm and persists
  a small meter adjustment (initial 0). UI cycles 0/-5/-10/-15/+5/+10 cm.
  The setting is not a substitute for correct Quest boundary-floor calibration.
- The private Hefu bundle intentionally strips all old HefuVR behaviours. The
  public client reattaches safe runtime bindings for exactly 7 hinged doors
  (entrance + 6 rooms) and 1 sliding balcony leaf by their known hierarchy.
  Handle/door-leaf ray hits use right Trigger in Explore; fixed door frames do not.
- Edit room: green floating marks above the 16 movable Hefu furniture groups,
  hover outline and text differentiate editable, door, and locked CAD; toggle
  markers on the editor toolbar. No renderer materials are modified permanently.
- Door animations do not change saved furniture transforms, and remain separate
  from Grip editing and XR tracked hands. Quest safety boundary remains enabled.
- Verification gates: editor compile/test, Unity Android build, local private
  install, headset testing for calibrated eye height, door hinge directions,
  blocker geometry, UI interactions, controller input ownership.

## B1 · Birthday scene anchored to real Hefu CAD (2026-10-23)

- Distinct `HefuVR Birthday` scene, selectable via Spaces > Birthday scene; switch back via
  Spaces > Original HefuVR. Original HefuVR room / saved furniture transforms remain intact.
- Real **north** entrance (`EntranceShoeCabinet`): welcome sign and balloons
  above cabinet, with no physical decoration collider in the entrance passage.
- North wall `DiningSideboard`: bouquet, wrapped gifts and LEDs.
- Dining `Move_Dining_Table`: layered cake and topper positioned
  from its actual CAD renderer bounds.
- South living `Move_Sofa` / `Move_Coffee_Table`:
  photo placeholders, balloon clusters, memory sign and floral centerpiece. Photo gallery
  faces west toward coffee table, not toward the balcony; offset toward room so it does not
  disappear into the wall.
- All B1 decorations are generated locally from primitives and bound to named furniture
  pivots at runtime. Moving the source furniture carries its decoration; no CAD walls,
  doors, or origin transforms are moved. `Celebrate lights` toggles local decorative LEDs.
- Placeholders `PHOTO 01-03` do not contain photos; real photos/audio need a separate
  local-only opt-in import, never a public GitHub commit, issue, or release.
- Physical decoration selection, per-ornament editing, media slideshow and automatic
  party sequence are **not yet implemented**. The B1 preset is static except its lights.
- Private EditorMac CAD smoke: five anchors verified against private furniture manifest,
  104 render objects added, scene scale preserved. All personal coordinates, private CAD
  renderings and local install artifacts remain on the user's Mac, never in the public repo.
  Android APK build and Quest device test are verified separately.

## B2 · Birthday editing on real Hefu furniture (2026-10-23)

- Birthday-only palette in `Spaces > Birthday scene > Edit room`: `+cake`, `+flowers`,
  `+gift`, `+balloon`, `+frame`. No images are included in frame placeholders.
- `Anchor` cycles Dining table / Sideboard / Coffee table / Shoe cabinet (real private
  CAD furniture objects identified by their saved manifest IDs). New items snap
  onto the true CAD renderer bounds, not an assumed table or room coordinate.
- Aim at an added ornament and use right Grip to move along the furniture surface;
  optional 5cm grid snap, with bounds clamping. Rotate +/- 15 degrees, scale +/- 5%,
  Delete and Undo reuse the existing spatial editor commands.
- Every added B2 object has an explicit `birthday/` whitelisted asset ID and stored
  `anchorId` in the same version-1 `.qpsspace` record. The saved transform is local
  to its furniture; moving furniture carries the ornaments. They are selectable
  trigger colliders so Quest locomotion isn't obstructed.
- The B1 backdrop remains unchanged and intentionally isn't freely deletable.
  `Original HefuVR` is a separate record; adding B2 decorations never alters it.
- 40-added-object cap; no arbitrary asset strings/paths or remote photo imports.
- Verified Unity PlayMode 156/156 and private EditorMac CAD integration for
  Add -> Save -> Switch original -> Reload birthday -> Undo. All private
  integration harnesses and private model bytes stay outside Git.
- Still requires Quest headset acceptance (hand ray, UI usability, placement and FPS).

## B2.1 · Better ornaments, local photos, palette, 1cm nudge (2026-10-23)

- Improved editable birthday geometries: two-layer cake with sugar pearls and stylized LED
  candle, more detailed roses/stems/vase, gift ribbon loops, balloon knot/base, photo frame
  with a correctly oriented front-facing photo Quad. These are still **lightweight
  procedural primitives**, not commercial-grade photorealistic 3D assets.
- Saved color accents per editable decoration: Blush / Champagne / Lavender / Sage.
  The frame can store an optional `birthdayPhotoSlot` from 0 (none) to 3.
- Photo slots read **only** app-private
  `SceneWorkspaces/Photos/slot-01.jpg` ... `slot-03.jpg` (PNG suffix also accepted).
  The B1 living-room gallery maps to the same slots. Missing slots show B1/B2 placeholders.
  Byte cap 4 MB/file, accepted image dimensions 16-2048 px, no URL or gallery permission.
  The filenames, image bytes and private CAD are never added to public Git.
- Fine adjustment UI adds X/Z ±1cm buttons on top of 5cm snap Grip dragging.
  Position edits clamp to the furniture's physical CAD surface and auto-save.
  Color/style and selected photo slot persist with B2 transforms in existing scene JSON.
- Local-only setup helper lives in the **private HefuVR project**, not QuestPhoneStream:
  `Scripts/install_birthday_photo_slot.sh {1|2|3} '/absolute/photo/path.jpg'`.
  It reduces photo to 1600px JPEG and pushes only after exactly one authorized ADB target.
- 159/159 PlayMode tests passing. Private EditorMac CAD smoke verifies B1 + B2 local
  photo textures, saved style/slot, switching original, and reloading the birthday scene.
- Hand interaction and FPS still require headset testing before release acceptance.

## B2.1 Quest Android birthday material regression (2026-10-23)

- Root cause verified from **real Quest Unity log**: selecting a persisted birthday
  scene failed with `Missing Unity birthday material shader`. Standard and
  `Unlit/Color` were accessed only through `Shader.Find`, and Android stripped
  those unreferenced shaders; `SceneWorkspaceManager` then fell back to another
  scene. The private room geometry and the 16 CAD furniture records survived.
- Added `QuestPhoneStream/BirthdayDecor`, a small Android-compatible, stereo-safe
  textured/color shader, and an explicitly serialized resource material under
  `Resources/Birthday/BirthdayDecor.mat`. All B1 procedural decorations, B2
  editable assets, and local photo slots use this packaged material (no stripped
  `Shader.Find("Standard")` dependency). Scene assembly does not throw merely
  because a cosmetic material is unavailable.
- Regression tests verify the material is found in `Resources` and supports both
  `_Color` and `_MainTex`; Unity PlayMode **168/168 passed**.
- Verified the built Quest development APK includes **both serialized material
  and shader binary asset bytes**. On-device confirmation still requires the
  `BIRTHDAY_READY` diagnostic after installing the repaired APK.
- All user-specific scene records, assets and rollback copies remain on the
  user's private Mac and Quest. No private Hefu CAD, photos or scene catalog are
  committed to this public repository.

## Quest render compatibility and lightweight birthday material polish (2026-10-23)

- Verified a real Quest 3S error `Desired shader compiler platform 18 is not available
  in shader blob`. Root cause: original private Hefu CAD AssetBundle was exported
  with Android OpenGL ES, while QuestPhoneStream runs Android Vulkan. Both use the
  same Unity Editor version. This was a material compatibility issue, not missing
  scene furniture or the B1 birthday generator.
- Private Hefu VR export now offers a *separate* Vulkan-only Android AssetBundle.
  Graphics API settings are restored in `finally`, keeping the standalone HefuVR
  phone project's OpenGL ES configuration. The new bundle has an identical manifest
  (all 16 movable furniture IDs and transforms; ceiling 2.7 m), and lives outside
  the public repo; original Quest CAD, scene and catalog files were backed up first.
- Confirmed on the actual Quest that the Vulkan bundle loads, prints
  `BIRTHDAY_READY anchors=5 decor=104`, restores 8 door interactions and no longer
  emits the graphics platform mismatch. Quest runtime stayed around 72/72 FPS.
- Improved the Quest's *serialized, Android-safe* birthday material shader with
  one analytic soft key light, diffuse shading, restrained specular for rose-gold
  accents and emission on birthday LEDs. This needs no extra realtime cast-shadow
  lights. Private photos use `_Unlit=1` to avoid unintended tint/brightness changes.
  Existing layout and scene protocol remain unchanged. 168/168 PlayMode tests pass.
- New Quest *developer* APK was built and verified, but must pass on-device
  rendering/performance acceptance before replacing the preceding known-good APK.
  Android wireless ADB can disappear when the headset suspends; never assume a
  streamed install succeeded without the device's actual `Success` response.

## B2.2 · Birthday art refinement and material pooling (2026-10-23)

- Adds more detailed, but still Quest-lightweight, geometric decoration to the existing
  birthday-v1 preset: elongated pink/ivory/gold balloons with tie knots, ribbon bow
  loops on gifts, rose hearts and alternating icing pearls on the dining-table cake.
  Furniture transforms, original CAD, scene records and locomotion remain unchanged.
- B2 edit-mode assets reuse their per-decoration `SceneWorkspaceBirthdayMaterialPool`
  keyed by quantized color, rather than generating a unique `Material` for every
  primitive. Style changes still use `MaterialPropertyBlock`; the pool cleans up
  shared material instances with the decoration root on scene unload.
- B1 memory-gallery placeholders and `PHOTO 01` text are hidden only when the local
  photo slot loads successfully. Images are neither read from remote URLs nor
  copied into the public repository.
- Unity PlayMode tests: 169/169 passed, including a regression verifying flower
  renderers share at most four materials. Quest Developer Tools Android APK built,
  signed and contains the B2.2 material pool and diagnostics components; private
  CAD and photos are not bundled. The **new** richer visual preset is not yet
  verified on Quest due to the headset's wireless ADB service disappearing.
- On next authenticated ADB connection, install the prebuilt B2.2 APK with `-r`,
  keep the currently installed Vulkan home.bundle, and verify BIRTHDAY_READY,
  Shader/IL2CPP errors and a sustained 72-FPS target. Compare against the last
  known-good app and roll back the APK if the richer decoration drops frames.

## B2.3 · Gallery styling and opt-in foreground Wi-Fi hint (2026-10-23)

- Preserves the real Hefu private room, five furniture anchor transforms, the
  version-1 birthday preset, the 3 local numbered photo slots and editability.
- Adds a single lightweight, 18-segment curved *ribbon mesh* at each of the
  entrance board, dining sideboard and living-room photo gallery. These use
  shared existing materials, disable shadows, have no colliders and own/destroy
  their Unity runtime meshes on scene unload. Floral clusters add a few scaled
  leaves without additional realtime shadow-casting lights. Two existing subtle
  birthday point lights remain the only B1 realtime lights.
- Added `QuestForegroundConnectionGuard`: disabled by default, explicitly
  toggled only in **Settings > Diagnostics > Keep Wi-Fi: OFF/ON** for developer
  APKs. It stores the user preference, while active requests Unity's
  `SleepTimeout.NeverSleep` and Android's low-latency foreground Wi-Fi lock.
  Native Wi-Fi lock needs the Android `WAKE_LOCK` permission, now included in
  the generated manifest. On application pause, disable or destroy it releases
  the lock and restores the preceding Unity timeout. If unavailable, it
  gracefully falls back to the screen hint.
- This **does not** disable the Horizon OS auto-sleep policy or guarantee ADB
  will remain online if the headset sleeps or the OS suspends the app. ADB TLS
  remains owned by the system. The headset's `screen_off_timeout` was already
  86400000 ms (24h), so raising that system value would not fix OS-level sleep.
  Keep the option off unless actively debugging; it can consume extra battery.
- PlayMode regression: **171/171 passed**, including guard lifecycle tests and
  ribbon mesh count, geometry and collision checks. The B2.3 Developer Tools
  APK was built with APK Signature v2, WAKE_LOCK granted on Quest, and neither
  private CAD nor photos were bundled. Quest install returned `Success`, and
  real scene startup printed `BIRTHDAY_READY anchors=5 decor=155`, without the
  older Vulkan shader-platform error. With the option off, last 20 runtime
  VrApi FPS samples averaged 72, minimum 70; startup had transient lower frames.
  Scene catalog and birthday save SHA256 were unchanged after upgrade.
- Android-side keep-awake acquire/release logging should additionally be checked
  after the user explicitly turns the new option **ON** on the headset.

## Birthday B2.4 – premium geometric detailing (2026-10-24)

- Preserve `birthday-v1` scene/schema, 17 persisted CAD furniture transforms, private numbered photo slots and existing independent birthday object records. No CAD/photos bundled into public APK or repository.
- Replace five sphere-like flower heads per bouquet with a pooled mesh of curled rose petal rings, folded bud centers, foliage and joined stems. Three bouquets now share three materials each rather than dozens of primitive renderers.
- Add a scalloped cake-icing mesh and a single three-candle mesh with warm tips. Keep the existing cake plate, pearled top and birthday writing.
- Replace full photo-frame boxes with four real moulding rails merged into one mesh per frame. The center remains open so numbered local photo surfaces remain visible. Balloon clusters receive one curved satin-string mesh each.
- All new meshes are collider-free, disable shadow casting/receiving, reuse the existing birthday materials and are explicitly destroyed on scene teardown. Runtime point-light count stays at two.
- Quest PlayMode: 174/174 PASS, including assertion for 11 pooled detail meshes, ribbon meshes, no XR-blocking colliders, shared-material count and unchanged furniture transforms. Quest Vulkan development APK built, signed and safely installed over USB; catalog + active ZUK-synced scene SHA-256 unchanged. Runtime FPS remains **not validated** because the headset was at Oculus sensor lock/Guardian after installation.
- Hefu Home mobile: version 0.3.17 (API 24) generated, signed, B2.4 editor smoke QA PASS with five anchors, 112 visual render objects, 17 movable furniture records and zero persistent scene leakage. Integrated QuestPhoneStream Android + Unity library debug APK built successfully, 24/24 Android unit tests. Mobile APK not installed on Quest or Android 10+ test handset.
- Three private Hefu editor snapshot angles were captured under `/Volumes/ssd/HefuVR-Android/Builds/BirthdayB24QA/`; do not commit the screenshots or user-home files.

## Birthday B2.5 + B2.6 — composition and refined interior styling (2026-10-24)

- **B2.5 spatial visibility**: scene-level CAD raycasts exposed a real obstruction: the south living gallery anchored at the sofa's old back was *behind an architectural wall* when viewed from the lounge. The birthday overlay now raycasts from the lounge toward the west face of the existing wall, placing the gallery ~9.5 cm on the visible side. The welcome grouping shifts toward the visible east side of the north cabinet and rotates to face the interior. It does **not** alter the actual walls, sofa, cabinet, furniture save transforms or source scene schema. The real private Hefu Editor sightline test passed (`HEFU_B25_WALL_VISIBILITY_PASS`).
- **B2.6 visual upgrade**: one curved textile + champagne trim backdrop at the gallery, one textile arch at the entry, an extra pair of low floral bouquets on both sides of the gallery, and one batched dining runner / small dessert-plate & macarons mesh. The BirthdayDecor shader gains low-amplitude woven variation and a soft pearl sheen, reusing the existing shader asset (no untrusted downloads or additional lights). The color theme remains muted ivory, blush pink, champagne gold, and foliage. All extra geometry is collider-free with shared materials and no new realtime light sources.
- **Visual QA**: three private 1024x768 editor preview images were captured for the gallery, entry and cake; correct composition was confirmed from gallery-west, welcome-east, and table camera viewpoints. `HEFU_B25_B26_VISUAL_QA_PASS threeSceneSnapshots=3 anchors=5 decor=121`. They remain under the private HefuVR Android project's `Builds/BirthdayB25B26QA/` and must not be uploaded publicly.
- **Quest QA**: 174/174 Unity PlayMode PASS, including geometry pooling/material slots and preserved furniture transforms. Signed B2.6 Quest APK produced and installed with `pm install -r` successfully after verifying the full-device transfer hash. The private scene catalog and active ZUK-synced scene SHA256 were identical before and after installation. The headset remained at `com.oculus.os.vrlockscreen/.SensorLockActivity`, so real-frame FPS and on-headset visual confirmation remain **pending**; a successful APK install alone is not a runtime performance check.
- **Android**: HefuVR standalone phone 0.3.18 (API 24) built and signed, 5 anchors / 121 decorations and cleanup QA PASS. Unified QuestPhoneStream Android sender + private Unity Library B2.6 built, 24/24 native unit tests PASS and APK v2 signed (API 29). Neither phone package was installed on an Android phone in this round. Neither personal home geometry, images, nor APKs were added to the public repository.
- **Risk**: The real CAD wall ray is geometry-specific to the Hefu birthday preset; generalized scanned rooms should use the generic SceneWorkspace scene logic and their own orientation metadata, not assume that the birthday overlay's wall is east-facing.
