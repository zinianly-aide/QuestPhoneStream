# QuestPhoneStream Project Analysis

## Project Overview
QuestPhoneStream is a system that streams an Android phone screen to Meta Quest 3/3S through WebRTC and renders it as a floating phone panel in Unity. Control commands are sent back over a WebRTC DataChannel.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Android Phone  │     │ Signaling Server│     │  Quest 3/3S     │
│    Agent        │────▶│   (Node.js)     │◀────│  Unity Client   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
        │                       │                       │
        ▼                       ▼                       ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ MediaProjection │     │ WebSocket       │     │ Unity WebRTC    │
│ Screen Capture  │     │ SDP/ICE         │     │ Video Render    │
│ WebRTC H.264    │     │ Forwarding      │     │ Control Channel │
│ Accessibility   │     │                 │     │ Input Mapping   │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

## Key Components

### Android Agent (`apps/android-agent`)
- **ScreenStreamService**: Captures screen using MediaProjection
- **WebRtcStreamer**: Handles WebRTC video streaming
- **SignalingClient**: Connects to signaling server
- **ControlCommand**: Handles control commands from Quest

### Signaling Server (`apps/signaling-server`)
- **WebSocket Server**: Handles device registration and session management
- **Protocol**: JSON-based signaling messages
- **Features**: SDP forwarding, ICE candidate relay, heartbeat

### Quest Unity Client (`apps/quest-unity-client`)
- **QuestSignalingClient**: Connects to signaling server via WebSocket
- **QuestWebRtcReceiver**: Receives WebRTC video stream
- **ControlChannel**: Sends control commands to Android
- **PanelInputMapper**: Maps Quest controller input to Android coordinates
- **PhonePanelController**: Controls the floating phone panel

## Dependencies

### Unity Packages
- `com.unity.webrtc`: 3.0.0-pre.8
- `com.unity.xr.openxr`: 1.14.1
- `com.unity.xr.interaction.toolkit`: 3.0.7
- `com.unity.inputsystem`: 1.11.2

### Android Dependencies
- `org.webrtc:google-webrtc:1.0.32006`
- `androidx.activity:activity-ktx:1.9.3`
- `com.squareup.okhttp3:okhttp:4.12.0`

## Configuration

### Default Values
- Signaling URL: `ws://<host-lan-ip>:8787`
- Token: `dev-token`
- Android Device ID: `android-phone-001`
- Quest Device ID: `quest-3s-001`
- Session ID: `local-session-001`

### Build Configuration
- Unity: 2022 LTS or Unity 6
- Android: API Level 29 (min), ARM64
- Scripting Backend: IL2CPP
- Graphics API: Vulkan

## Current Issues

### Settings UI Visibility
- WorldSpace Canvas not showing in VR
- Canvas position needs to follow camera
- Controller input not mapped correctly

### Connection Issues
- WebSocket connection to signaling server fails
- IP address not updated in APK
- Clash proxy interference on Quest

## Next Steps
1. Fix Settings UI visibility in VR
2. Implement proper XR Origin setup
3. Add controller input handling
4. Test end-to-end streaming
5. Document lessons learned
