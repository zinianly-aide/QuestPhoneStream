# macOS Sender

QuestPhoneStream 的 macOS 屏幕发布端（Electron + WebRTC）。

## 已支持能力

- **屏幕发布**：Electron `desktopCapturer` + WebRTC 推流，目标 1920×1080@30fps（以实际 negotiated 为准）。
- **NSD 发现**：`_qps-device._tcp.` 广播（TXT 含 `id/name/caps/capv/streamId/signalingUrl/platform/sourceType`），唯一服务名 + `probe:false`，重启不重复注册、退出正确释放。
- **Capability Discovery**：注册后通过 Spatial `device.capabilities.get/result` 发布 `display.publish` capability。
- **Signaling 配置优先级**：持久化手动配置 > NSD 发现端点 > 环境变量 `QPS_SIGNALING_URL` > 历史配置 > 无端点（进入 Waiting / Configure 状态）。**不内置任何固定 IP 回退**。
- **Spatial 通道**：
  - `spatial-fast`（unreliable）：`xr.head.pose` / `xr.controller.pose` / `xr.hand.pose`
  - `spatial-reliable`（reliable）：`spatial.anchor`
  - 订阅跟踪、去重、重建、重连清理。
- **配置持久化**：`userData/config.json`（signaling URL、token、questDeviceId、sessionId），`device-id.txt` 持久化 deviceId。

## 明确未实现

- Mac 音频推流
- Quest → Mac 远程控制

## 运行

```bash
npm install
npm start
```

可选环境变量：

| 变量 | 说明 |
| --- | --- |
| `QPS_SIGNALING_URL` | 信令服务 WebSocket 地址（如 `ws://192.168.1.16:8787`），优先级低于 NSD 发现与持久化手动配置 |
| `QPS_SIGNALING_TOKEN` | 信令 token，默认 `dev-token`（与服务端默认一致） |
| `QPS_DEVICE_ID` | 覆盖持久化 deviceId |
| `QPS_QUEST_DEVICE_ID` | 预设配对 Quest deviceId |
| `QPS_SESSION_ID` | 预设 sessionId |

macOS 首次推流会请求「屏幕录制」权限（系统设置 → 隐私与安全性 → 屏幕录制）。音频与远程控制未实现。

## 打包

```bash
npm run pack        # 生成 release/mac-arm64/QuestPhoneStream Mac.app
npm run dist:mac    # 额外生成 .dmg
```

产物未签名、未公证（本地安装即可运行；如被 Gatekeeper 拦截，右键打开或 `xattr -cr` 后重试）。

## 测试

```bash
npm test
npm run build
```
