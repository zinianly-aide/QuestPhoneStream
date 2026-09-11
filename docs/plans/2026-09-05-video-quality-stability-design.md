# Quest 视频画质与 WebRTC 生命周期设计

## 目标与边界

当前实机已经能稳定接收并显示 720×1280 手机画面，Quest 维持 72 FPS，但 Standard 受光材质把屏幕染成明显的黄褐色；Android 重连路径还会把 `DataChannel.dispose()` 延迟到 PeerConnectionFactory 释放之后，存在已复现的 native use-after-free。此次只解决视频呈现准确性、帧更新时序和连接销毁安全性，不调整分辨率、码率、交互布局或信令协议。

## Quest 呈现设计

项目内新增 `QuestPhoneStream/UnlitVideo` Shader，由 `PhonePanel.mat` 显式引用。Shader 不参与场景光照，保持视频原始颜色；使用双面渲染，继续兼容 Quad 朝向变化，并支持 Vulkan 单通道立体实例化。场景生成器始终校验该材质，避免重新生成场景后退回 Standard。运行时只使用 `sharedMaterial`，确保 Receiver 写入的 `_MainTex` 就是 Renderer 实际采样的纹理。

`OnVideoReceived` 只保存 WebRTC 源纹理、准备目标 RenderTexture 并完成材质绑定。独立协程在 `WaitForEndOfFrame` 后持续 `Graphics.Blit`，首次真正 Blit 完成后才上报 `MediaConnected`。这与 Unity Render Streaming 的接收路径一致，可避开 Vulkan 外部纹理在回调时尚未完成 GPU 更新的问题。

## Android 生命周期设计

`DataChannel` 只注销 observer 并关闭，不再单独调用 native `dispose()`；其底层所有权交给 PeerConnection。旧 PeerConnection 保留 250ms 回调排空窗口，并由一个可测试的延迟销毁队列统一管理。全局 `dispose()` 先停止采集、挂起底层资源释放，等所有新旧 PeerConnection 都完成销毁后，再按 track/source/helper/factory/EGL 顺序释放。这样重连防抖仍然有效，同时不会让延迟任务访问已经释放的 factory/EGL。

## 验证

Unity PlayMode 测试检查 Unlit Shader、共享材质和面板朝向；Android JVM 测试检查延迟销毁、去重和“队列排空后才 final release”。随后构建并安装双端 APK，循环重启 Quest 会话，确认无 SIGSEGV/SIGABRT，最后用 Quest 截图检查画面颜色与双眼呈现。
