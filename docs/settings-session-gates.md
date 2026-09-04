# Settings / Session 修复与验收记录

基线：`31c1fb2739f6276d646dea5fe5fcef36e6feb202`。工作分支：`fix/settings-session-gates`。

## 结论

**GATE-1：NOT VERIFIED；GATE-2：NOT VERIFIED。均不得标记 PASS。**

代码已修改，服务端自动测试和源码检查通过；Unity 编译、PlayMode、Android APK 构建、Quest/手机端到端操作尚未通过验证。以下内容不是产品完成声明。

## 修改前的实际链路

- Scene 已序列化 QuestSignalingClient；其 Awake 读取设置。Receiver 创建活跃对象并 AddComponent SettingsUIFactory 后才赋 signalingClient，Factory 的 Awake 已先复制空引用。
- Factory 创建 UI 后 Show，SettingsUI 的 Start 又 Hide。Hide 操作 Canvas，但 Receiver 读取 SettingsUI 根对象的 activeSelf，导致重新打开判断错误。
- Canvas 采用约 2×1.5 的布局空间，却使用普通 UI 字号和 padding。Scene 相机没有 MainCamera 标签，SettingsUI 又依赖 Camera.main。
- Scene 没有完整 XR Origin、双控制器射线、EventSystem/XR UI 模块和对应 actions；菜单使用旧 Input Manager 按键。
- Reconnect 只重开 WebSocket 并发送 register，没有等待注册 ACK 后主动恢复 Session。Android 在 WebSocket open 发起 Session，在 session_created 发 offer，PeerConnection 没有按重连代次重建。

## GATE-1 修改范围

- Factory 改为 Initialize(client, camera)，无 Awake/Start 注入；SettingsUI 只初始化一次。
- Show/Hide/Toggle 和 IsVisible 统一操作 Canvas，初始隐藏，不存在后续 Start 覆盖 Show。
- Canvas 布局 1000×750，scale 0.002，约 2×1.5 米。输入文字 22、Label/按钮 24，padding 保留正文本区域。
- 相机由 Scene/Receiver 显式传入，不要求 MainCamera 标签。Show 时置于头部前方约 2 米，水平朝向；不逐帧跟头，下一次打开重新定位。丢失相机只报错一次。
- Scene 序列化 QuestXrUiRig 与 QuestUi.inputactions。启动时创建标准 XROrigin、TrackedPoseDriver、XRInteractionManager、双 XRRayInteractor、XRInteractorLineVisual、EventSystem 和 XRUIInputModule；Canvas 使用 TrackedDeviceGraphicRaycaster。
- Input System 为唯一输入后端。右手 primaryButton（Touch A）开关菜单，左右手 aim pose 指向，trigger 选择。动作资产预先配置，不在启动后动态新增绑定。
- 输入资产的加载/绑定约束参见 [Unity OpenXR Input 文档](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/input.html)。实际采用项目锁定的 Input System 1.11.2、XRI 3.0.7 和 OpenXR 1.14.1，未升级这些依赖。

## GATE-2 修改范围与协议

Connect 禁止重入，禁用编辑/按钮并 await ReconnectAsync。状态区分 WebSocket、注册、Session、PeerConnected 与 MediaConnected；失败为固定安全文本，不输出 token/SDP。

重连顺序：

1. 废弃旧 negotiation；清除旧 ICE/协程/纹理引用，关闭并释放 DataChannel/PeerConnection 和旧 WebSocket。
2. 建立 WebSocket，发送 register，等待匹配 deviceId/role 的 registered ACK。拒绝认证只进入 AuthFailed。
3. Quest 发送现有 create_session；服务端验证在线身份和设备配对，先通知 Quest，再通知 Android。
4. Android 收到新 session_created 后创建新 PeerConnection/DataChannel 和 fresh offer；录屏授权与 Capture 保留，避免重复消费 MediaProjection 授权。
5. Quest 应答；两端在 remote SDP 设置成功前缓存 ICE。回调和消息都检查当前协商代次。
6. PeerConnection Connected 只进入 PeerConnected；收到视频纹理后才进入 MediaConnected。渲染每帧复制当前视频纹理，避免只显示第一帧。

复用 create_session/session_created/offer/answer/ice/registered，不增加新的请求类型，不存储或重放旧 offer。最小协议扩展是可选 negotiationId：

- Quest 每次 Reconnect 生成新的 ID，在 create_session 中发送。
- Server 的 session_created 及有会话上下文的错误/离线通知携带该 ID；SDP/ICE 必须与当前 Session 的 ID 完全一致。
- ID 不相同或省略 ID 不能向已升级会话转发消息。Android 使用通知中的 ID 回传；旧协商的异步回调被丢弃。
- 原有无 ID 的双方仍可建立 legacy Session；legacy 模式不提供同一 Session 的代次隔离保证。完整 GATE-2 必须部署本轮 Server、Quest、Android 三端。
- Android 延迟到达的 bootstrap 请求不能把 Quest 已创建的会话降级为无 ID。
- 关闭或替换注册 socket 时删除该设备的 Session，通知对端结束。Quest 重连后可沿用 Session ID 并切换手机，旧设备消息不能进入新会话。
- 手机未在线时立即 DeviceOffline，不暗中无限重试；手机上线后用户再次 Connect。信令阶段默认 10 秒超时，媒体阶段默认 30 秒。

## 上一轮实际执行结果

| 检查 | 结果 | 证明范围 |
| --- | --- | --- |
| Signaling Vitest 集成测试 | PASS，15/15 | 真实本地 WebSocket 注册、ACK、离线、重连、旧 SDP/ICE 拒绝、Session/设备切换、身份限制及日志脱敏 |
| TypeScript tsc --noEmit | PASS | Server 类型检查 |
| node --test scripts/check-settings-gates.mjs | PASS，8/8 | 源码契约、显式依赖、可见性入口、尺寸计算、XR 组件/action 接线、ACK 顺序、旧状态清理、Manifest XML |
| git diff --check | PASS | 补丁空白检查 |
| Unity PlayMode 测试 | NOT RUN，新增 14 个 | 本环境没有 Unity Editor；不能认定 C# 编译或测试通过 |
| Android :app:assembleDebug | BLOCKED | Gradle 8.10.2 可启动，但配置的仓库未能解析 com.android.application:8.7.3；尚未进入 Kotlin 编译 |
| Quest/Android 真机 | NOT RUN | 本环境无 ADB 与已连接设备 |

Test Framework 1.1.33 / NUnit 1.0.6 的 manifest 和 lock 已依据官方包依赖同步，但还没有由 Unity Package Manager 实际导入确认。

已执行命令（前两条在 apps/signaling-server，其余在仓库根目录）：

```bash
node node_modules/vitest/vitest.mjs run
node node_modules/typescript/bin/tsc --noEmit
node --test scripts/check-settings-gates.mjs
git diff --check
```

Android 构建命令（在 apps/android-agent）：

```bash
./gradlew :app:assembleDebug --console=plain
```

## 标记 PASS 前必须补完

- 在安装项目所需 Unity Editor 的机器打开 apps/quest-unity-client，完成包导入和编译，在 Test Runner 的 PlayMode 执行 QuestPhoneStream.PlayModeTests，保留 XML 结果。14 个测试覆盖初始化、10 次开关、布局、相机丢失、世界定位、XR 输入链路、ACK、失败与过期消息、连接重入及超时。
- 修复构建环境的 Android Gradle Plugin 依赖解析后完成 APK 构建；本轮未更改仓库来源或插件版本来绕开阻塞。
- 真机验证右 A 开/关菜单至少 10 次；转头菜单留在原位，重新打开在新朝向前方；双手射线可以点击、编辑字段并使用系统键盘；Label、当前默认值和按钮文字可读。任意长自定义值仍需验证输入框滚动编辑行为。
- 错 token：不得出现 Registered/Connected 成功状态，显示 Authentication failed，按钮可再次使用。
- 手机离线：显示 Phone is offline；手机上线后 Connect 可以启动新 offer，不能依赖旧 offer 缓存。
- 正常 Connect 与重复 Reconnect：分别记录 registered、session_created、fresh offer/answer、ICE、PeerConnected、实际持续更新的视频；不能用只收到首帧或仅 WebSocket 通来代替通过。
- 连续点击只能有一个尝试；切换 Session、切换手机（包括保留 Session ID）、断开旧 socket 后重连，旧视频/控制通道/ICE 不得继续生效；失败和超时后按钮恢复可操作。
- 录屏权限撤销、Quest 休眠/恢复、真实弱网下的 Peer 状态与资源释放仍需设备回归。没有这些证据，两项 Gate 保持 NOT VERIFIED。

截至上述验证结束，没有提交或推送。保留了原有、不属于本轮的未跟踪 pnpm 文件。

## 2026-09-04 验证续轮：环境阻塞，未修改业务代码

### 工作区与执行命令

本轮开始与结束均为 fix/settings-session-gates，HEAD 仍是原始基线；未 reset、clean、切 main、提交、推送或合并。开始时 21 个 tracked modified、23 个 untracked 文件。业务补丁保持 21 files changed, 1177 insertions(+), 708 deletions(-)。本轮只更新本报告并保存验证日志。

实际执行：

```bash
# 仓库根目录
git status
git branch --show-current
git diff --check
git diff --stat
git diff
java -version
cat apps/quest-unity-client/ProjectSettings/ProjectVersion.txt
node --test scripts/check-settings-gates.mjs
adb devices

# apps/android-agent
./gradlew --version
./gradlew tasks --stacktrace --info --console=plain
./gradlew :app:assembleDebug --stacktrace --info --console=plain

# apps/signaling-server
node node_modules/vitest/vitest.mjs run
node node_modules/typescript/bin/tsc --noEmit
```

另外执行 command -v 与 rg --files 检查 Unity/ADB/SDK 安装，读取 Kotlin Gradle 配置、Unity 构建入口、Scene/XR 配置及核心实现和 14 个测试；使用 unzip -tqq 检查 Gradle JAR/ZIP 完整性，df 检查磁盘与 inode，curl 检查原有仓库与官方 Gradle 下载地址。

完整构建错误：[gradle-tasks-initial.log](verification-2026-09-04/gradle-tasks-initial.log)、[gradle-build-initial.log](verification-2026-09-04/gradle-build-initial.log)。两条 Gradle 命令退出码均为 1，不是超时后人为判为失败。

### Android / AGP root cause

本轮复现的第一个错误是 `Failed to create Jar file .../generated-gradle-jars/gradle-api-8.10.2.jar`，底层异常为 `java.util.zip.ZipException: zip END header not found`。遍历分发包 JAR 后确认损坏文件为：

`apps/android-agent/.gradle/local/gradle-8.10.2/lib/plugins/junit-platform-launcher-1.8.2.jar`

原下载 ZIP 也无法通过 unzip 完整性检查（56,187,904 bytes，缺少 central directory）；磁盘剩余约 30GB，inode 使用率 1%。这属于 **Gradle 分发包不完整/损坏**，发生在 Kotlin DSL classpath 生成阶段，早于 AGP 解析。因此上一轮“AGP 8.7.3 无法解析”的更深根因本轮没有独立复现，不能把当前异常伪装成 AGP 版本错误。

环境与分类：

- Java：OpenJDK 17.0.20；Gradle：8.10.2；AGP：8.7.3；Kotlin Android plugin：1.9.25。
- 项目并无 gradle-wrapper.properties；gradlew 是自定义启动脚本，默认下载 Gradle 8.10.2，检测到可执行文件后直接使用。本轮启动成功不等于分发包完整。
- pluginManagement 已有项目原有阿里云仓库、google()、mavenCentral()、gradlePluginPortal()；dependencyResolutionManagement 已有原有阿里云仓库、google()、mavenCentral()。不是缺少标准仓库声明；没有删除或替换仓库。
- Google Maven 的 AGP 8.7.3 POM HEAD 请求 15 秒超时；原有阿里云 google 仓库同一 AGP POM HEAD 返回 HTTP 200。这只证明该请求结果，不证明 Gradle plugin marker 和完整传递依赖可以解析。
- 代理环境变量存在；未输出其值。尚未执行到 Gradle 的仓库下载阶段，未确认 JVM 代理链路；不能将代理配置猜测当成已证实根因。
- 从原官方 services.gradle.org 地址获取同版本分发包时，执行环境返回 `network approval was cancelled before a decision was returned`。立即停止；没有换域名、换镜像或绕过权限继续下载。恢复分发包现为 **BLOCKED_BY_DEPENDENCY_NETWORK / approval**。
- SDK：ANDROID_HOME、ANDROID_SDK_ROOT 未设置，无 local.properties，检查 PATH、常用安装目录及 /opt、/usr/local、/usr/lib、/workspace 未找到 sdkmanager/android.jar。compileSdk/targetSdk=36，buildToolsVersion=36.1.0。SDK 缺失是独立后续阻塞，不是本次 ZIP 异常的原因。
- 这是独立手写 Kotlin Android 工程，不是 Unity 导出的工程；未发现本次失败由 Unity Android tooling 冲突引起的证据。
- [AGP 8.7 官方说明](https://developer.android.com/build/releases/agp-8-7-0-release-notes)要求 Gradle 至少 8.9、JDK 17，当前二者符合最低要求；其支持的最高 API 为 35，项目 compileSdk 36 并设置了 suppressUnsupportedCompileSdk。这是尚待构建验证的兼容性风险，不是当前异常根因，本轮未改版本或 SDK target。

**Android Build = BLOCKED。** Gradle configuration 未通过；Kotlin/Android compile 未执行；没有产出 APK，也没有本轮最终 merged manifest。源码 Manifest 的 XML parse 通过，不能替代 manifest merge 成功。

需要的环境改变：允许获取并校验完整的原版 Gradle 8.10.2 分发包及项目依赖，提供 SDK Platform 36/Build Tools 36.1.0；之后才能复跑并继续隔离历史 AGP artifact resolution 问题。

### Unity / Quest build / 设备

项目要求 `2022.3.62f3c1 (1623fc0bbb97)`。PATH 与上述安装目录检查未发现 Unity Editor，未切换 Unity 大版本，也未修改项目序列化配置。

| 检查 | 本轮结果 |
| --- | --- |
| Signaling 集成测试 | PASS，15 passed / 0 failed / 0 skipped |
| Source contract | PASS，8 passed / 0 failed / 0 skipped |
| TypeScript / git diff --check | PASS |
| Unity EditMode | NOT RUN；未发现单独 EditMode 测试；passed/failed/skipped=N/A，无 XML |
| Unity PlayMode | NOT RUN；14 个源码定义，passed/failed/skipped=N/A，无 XML |
| Quest Android APK build | NOT RUN；无 Unity Editor / SDK / NDK，APK path/size=N/A |
| adb devices | 命令退出 127：adb: command not found |
| Quest / Phone | 均 unavailable；没有实际确认连接的设备 |
| GATE-1 | NOT VERIFIED；controller hover/click/focus 全部 NOT RUN |
| GATE-2 | NOT VERIFIED；设备 fresh offer、双向 ICE、重连媒体链路全部 NOT RUN |

静态配置可见 ARM64、Input System、OpenXR Oculus Touch 与 Meta Quest feature（含 Quest 3S）；这不能证明 APK 构建或设备运行。

14 个 PlayMode 测试分布为 SettingsUiTests 5、SignalingTests 8、XrUiRigTests 1。它们确实创建 Unity GameObject、调用生产 Initialize/Show/Hide/Toggle 与信令 handler，不是单纯源码字符串检查。但信令测试使用反射注入部分状态，**没有覆盖真实 Disconnected→WebSocketConnecting→Registering 的网络启动全过程，也没有直接验证原生 PeerConnection/DataChannel Dispose 或真实 Reconnect 发出新 create_session**。过期代次隔离与重入有测试定义，不能因此宣称完整生命周期已经运行验证。后续仍需实际 Unity/设备证据。

### 本轮最小修复记录

- Failure：两次 Gradle 命令均遇损坏 JAR。
- Root cause：现有 Gradle 分发包 ZIP/JAR 完整性失败。
- File changed：没有业务代码、Gradle 配置或二进制修复；只更新本报告并新增完整错误日志。
- Minimal action：尝试原官方同版本下载，因网络授权未完成而停止；没有降级 AGP、改业务实现或清理用户文件。
- Verification after change：无代码修复可宣称验证；可运行测试重跑通过，Android、Unity、设备仍阻塞。

继续之前需要提供可用的 Unity 2022.3.62f3c1/Android 工具链和连接设备的执行环境，并解决官方下载权限；本轮没有远程访问用户电脑或设备。

## 验证后的代码交付

用户随后授权将现有修改提交并推送到 fix/settings-session-gates；不合并 main。交付范围包括修复、测试、本报告和构建失败日志，不包含原有的无关 pnpm 文件或构建工具缓存。提交/推送不改变上述验收结论：GATE-1 与 GATE-2 仍为 NOT VERIFIED。
