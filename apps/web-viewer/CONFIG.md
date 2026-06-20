# QuestPhoneStream Remote Viewer — 配置参考

## 1. Tailscale 配置

### 安装
- Mac:   https://tailscale.com/download → 下载 macOS 版
- Phone: Google Play 搜索 Tailscale 安装

### 确保两台设备在同一 Tailscale 网络
```bash
# Mac 上运行, 确认已登录
tailscale status

# 记下 Mac 的 Tailscale IP
tailscale ip -4
# 示例输出: 100.82.xxx.xxx
```

### Tailscale ACL (可选, 默认策略已允许)
如果你的 Tailscale 网络有自定义 ACL, 在 admin console 添加:

```jsonc
{
  "acls": [
    // 允许所有同 tailnet 设备互通 (默认已有)
    {"action": "accept", "src": ["*"], "dst": ["*:*"]}
  ]
}
```

无需额外配置, 安装登录即用。

---

## 2. Android Agent 配置

在 QuestPhoneStream App 中填写:

| 字段 | 值 |
|------|-----|
| Signaling URL | `ws://<MAC_TAILSCALE_IP>:8787` |
| Token | `dev-token` |
| Android Device ID | `android-phone-001` |
| Quest Device ID | `quest-3s-001` |
| Session ID | `local-session-001` |

> 将 `<MAC_TAILSCALE_IP>` 替换为 Mac 的 Tailscale IP (如 `100.82.123.45`)
>
> 如果 APK 不支持 ws:// (明文限制), 改用 `wss://<MAC_TAILSCALE_IP>:8787` 并在 Mac 端设置 `SIGNALING_USE_WSS=1`

操作:
1. 在 App 中填写上述配置
2. 点击 **Start Screen Stream**
3. 授权屏幕录制

---

## 3. Web Viewer 配置

浏览器打开后自动配置, 无需手动操作。

高级用途: 可以通过 URL 参数覆盖默认值:
```
http://127.0.0.1:8787?url=ws://other-host:8787&token=custom-token&deviceId=quest-custom&sessionId=custom-session
```

---

## 4. 签名服务器 WSS 模式 (可选)

仅当 Android APK **未**启用 cleartext traffic 时需要:

```bash
# Mac 上启动时启用 WSS
SIGNALING_USE_WSS=1 ./scripts/remote-mac.sh
```

此选项会自动生成自签名证书。浏览器会显示证书警告, 点击"继续访问"即可。

---

## 网络拓扑

```
┌─ Android Phone ─────────────────────┐
│  Tailscale IP: 100.x.x.a            │
│  ws://100.x.x.b:8787 (signaling)    │
│  P2P to 100.x.x.b (WebRTC media)    │
└────────────┬────────────────────────┘
             │ Tailscale WireGuard
             │
┌────────────┴────────────────────────┐
│  Mac Mini                            │
│  Tailscale IP: 100.x.x.b            │
│  :8787 — signaling + web viewer     │
│  Chrome → http://127.0.0.1:8787     │
└──────────────────────────────────────┘
```
