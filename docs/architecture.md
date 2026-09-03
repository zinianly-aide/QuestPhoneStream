# QuestPhoneStream Architecture

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Network Layer                            │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │ Android Phone │    │   Signaling  │    │   Quest 3    │      │
│  │    Agent      │◀──▶│   Server     │◀──▶│ Unity Client │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Data Flow                                  │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │ MediaProjection│   │   WebSocket  │    │ Unity WebRTC │      │
│  │ Screen Capture │──▶│   Signaling  │──▶│ Video Render │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
│                                                                 │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐      │
│  │ Accessibility │    │   DataChannel│    │   Input      │      │
│  │   Service     │◀──│   Control    │◀──│   Mapping    │      │
│  └──────────────┘    └──────────────┘    └──────────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

## Component Architecture

### Android Agent
```
┌─────────────────────────────────────────────┐
│              Android Agent                  │
├─────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐          │
│  │ MainActivity │  │ ScreenStream│          │
│  │             │  │   Service   │          │
│  └─────────────┘  └─────────────┘          │
│         │               │                   │
│         ▼               ▼                   │
│  ┌─────────────┐  ┌─────────────┐          │
│  │  Signaling  │  │  WebRTC     │          │
│  │   Client    │  │  Streamer   │          │
│  └─────────────┘  └─────────────┘          │
│         │               │                   │
│         ▼               ▼                   │
│  ┌─────────────┐  ┌─────────────┐          │
│  │  WebSocket  │  │  MediaCodec │          │
│  │   Connection│  │  H.264      │          │
│  └─────────────┘  └─────────────┘          │
└─────────────────────────────────────────────┘
```

### Signaling Server
```
┌─────────────────────────────────────────────┐
│           Signaling Server                  │
├─────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐          │
│  │  WebSocket  │  │  Protocol   │          │
│  │   Server    │  │  Handler    │          │
│  └─────────────┘  └─────────────┘          │
│         │               │                   │
│         ▼               ▼                   │
│  ┌─────────────┐  ┌─────────────┐          │
│  │  Device     │  │  Session    │          │
│  │  Registry   │  │  Manager    │          │
│  └─────────────┘  └─────────────┘          │
│         │               │                   │
│         ▼               ▼                   │
│  ┌─────────────┐  ┌─────────────┐          │
│  │  SDP/ICE    │  │  Heartbeat  │          │
│  │  Forwarding │  │  Monitor    │          │
│  └─────────────┘  └─────────────┘          │
└─────────────────────────────────────────────┘
```

### Quest Unity Client
```
┌─────────────────────────────────────────────┐
│           Quest Unity Client                │
├─────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐          │
│  │  Quest      │  │  Quest      │          │
│  │  Signaling  │  │  WebRTC     │          │
│  │  Client     │  │  Receiver   │          │
│  └─────────────┘  └─────────────┘          │
│         │               │                   │
│         ▼               ▼                   │
│  ┌─────────────┐  ┌─────────────┐          │
│  │  Control    │  │  Panel      │          │
│  │  Channel    │  │  Input      │          │
│  │             │  │  Mapper     │          │
│  └─────────────┘  └─────────────┘          │
│         │               │                   │
│         ▼               ▼                   │
│  ┌─────────────┐  ┌─────────────┐          │
│  │  Phone      │  │  Settings   │          │
│  │  Panel      │  │  UI         │          │
│  │  Controller │  │             │          │
│  └─────────────┘  └─────────────┘          │
└─────────────────────────────────────────────┘
```

## Data Flow

### 1. Video Streaming Flow
```
Android Phone → MediaProjection → H.264 Encoder → WebRTC → Quest Unity → RenderTexture
```

### 2. Control Command Flow
```
Quest Controller → Input System → PanelInputMapper → ControlChannel → DataChannel → Android Accessibility Service
```

### 3. Signaling Flow
```
Quest ←→ Signaling Server ←→ Android
  │           │                │
  │           │                │
  ▼           ▼                ▼
Register    Session         Register
  │         Creation          │
  │           │                │
  ▼           ▼                ▼
Offer/Answer Forwarding    Offer/Answer
  │           │                │
  ▼           ▼                ▼
ICE Candidate Relay        ICE Candidate
```

## Key Technologies

### 1. WebRTC
- **Video Codec**: H.264 (hardware accelerated)
- **Transport**: UDP with ICE/STUN/TURN
- **Latency Target**: <300ms end-to-end

### 2. Unity XR
- **Runtime**: OpenXR
- **SDK**: Meta XR SDK
- **Input**: Input System with controller mapping
- **Rendering**: URP with Vulkan

### 3. Android
- **Screen Capture**: MediaProjection API
- **Accessibility**: AccessibilityService for control
- **WebRTC**: Google WebRTC library

### 4. Signaling
- **Protocol**: JSON over WebSocket
- **Port**: 8787 (default)
- **Authentication**: Token-based

## Configuration

### Environment Variables
```bash
SIGNALING_HOST=0.0.0.0
SIGNALING_PORT=8787
SIGNALING_TOKEN=dev-token
HEARTBEAT_TIMEOUT_MS=45000
PING_INTERVAL_MS=15000
```

### Unity Settings
- **Target Platform**: Android
- **Architecture**: ARM64
- **Graphics API**: Vulkan
- **Scripting Backend**: IL2CPP
- **Minimum API Level**: 29

## Security Considerations

### 1. Network Security
- Use WSS for production
- Implement proper authentication
- Validate all incoming messages

### 2. Permissions
- MediaProjection requires user consent
- Accessibility Service requires manual enable
- Network permissions required

### 3. Data Privacy
- Screen content is streamed over network
- Control commands are sent over DataChannel
- No persistent storage of video data

## Performance Considerations

### 1. Video Quality
- Resolution: 720p/1080p
- Frame Rate: 30fps
- Bitrate: Adaptive based on network

### 2. Latency
- Target: <300ms end-to-end
- Optimization: Hardware encoding/decoding
- Network: Local WiFi preferred

### 3. Battery
- Screen capture is CPU intensive
- WebRTC encoding/decoding uses GPU
- Consider power management

## Future Enhancements

### 1. Multi-Device Support
- Multiple Android phones
- Multiple Quest headsets
- Session management

### 2. Advanced Controls
- Gesture recognition
- Voice commands
- Keyboard input

### 3. Quality Improvements
- Adaptive bitrate
- Error correction
- Reconnection logic
