# Reusable Components

## Unity Scripts

### 1. UnityMainThread
**Purpose**: Thread-safe action queue for Unity main thread
**Location**: `Assets/QuestPhoneStream/Scripts/UnityMainThread.cs`
**Usage**:
```csharp
UnityMainThread.Enqueue(() => {
    // Run on main thread
});
```
**Features**:
- ConcurrentQueue for thread safety
- DontDestroyOnLoad persistence
- RuntimeInitializeOnLoadMethod auto-install

### 2. SignalingMessages
**Purpose**: Data transfer objects for signaling protocol
**Location**: `Assets/QuestPhoneStream/Scripts/SignalingMessages.cs`
**Classes**:
- `SignalMessage`: WebRTC signaling messages
- `IceCandidateDto`: ICE candidate data
- `ControlCommandDto`: Control commands (click, swipe, back, text)

### 3. QuestSignalingClient
**Purpose**: WebSocket client for signaling server
**Location**: `Assets/QuestPhoneStream/Scripts/QuestSignalingClient.cs`
**Features**:
- Auto-reconnect
- Heartbeat loop
- PlayerPrefs settings persistence
- Event-based message handling

### 4. ControlChannel
**Purpose**: WebRTC DataChannel for control commands
**Location**: `Assets/QuestPhoneStream/Scripts/ControlChannel.cs`
**Methods**:
- `SendClick(x, y)`: Send click command
- `SendSwipe(startX, startY, endX, endY, durationMs)`: Send swipe command
- `SendBack()`: Send back button command
- `SendText(text)`: Send text input command

### 5. PanelInputMapper
**Purpose**: Maps Quest controller input to Android coordinates
**Location**: `Assets/QuestPhoneStream/Scripts/PanelInputMapper.cs`
**Features**:
- Raycast from camera to panel
- UV to Android coordinate mapping
- InputAction integration

### 6. PhonePanelController
**Purpose**: Controls floating phone panel behavior
**Location**: `Assets/QuestPhoneStream/Scripts/PhonePanelController.cs`
**Features**:
- Scale up/down
- Follow anchor
- Reset scale

## Scripts

### 1. check-quest-env.sh
**Purpose**: Check Quest development environment
**Location**: `scripts/check-quest-env.sh`
**Checks**:
- macOS version
- Java version
- ADB installation
- Android SDK
- Unity editors
- Connected Quest devices

### 2. quest-adb-debug.sh
**Purpose**: ADB debugging workflow
**Location**: `scripts/quest-adb-debug.sh`
**Features**:
- Device detection
- APK installation
- Log clearing
- App launching
- Logcat capture

### 3. quest-wireless-adb.sh
**Purpose**: Enable wireless ADB
**Location**: `scripts/quest-wireless-adb.sh`
**Steps**:
- Enable TCP/IP mode
- Get Quest IP address
- Provide connection command

## Patterns

### 1. Thread-Safe UI Update
```csharp
// From background thread
UnityMainThread.Enqueue(() => {
    // Update UI on main thread
    Debug.Log("Updated");
});
```

### 2. WebRTC Signaling Flow
```csharp
// 1. Connect to signaling server
await signalingClient.ConnectAsync();

// 2. Register device
await signalingClient.SendAsync(new SignalMessage {
    type = "register",
    token = "dev-token",
    role = "quest",
    deviceId = "quest-3s-001"
});

// 3. Handle offer/answer
signalingClient.MessageReceived += message => {
    switch (message.type) {
        case "offer": // Handle offer
        case "answer": // Handle answer
        case "ice": // Handle ICE candidate
    }
};
```

### 3. Control Command Pattern
```csharp
// Click at coordinates
controlChannel.SendClick(x, y);

// Swipe gesture
controlChannel.SendSwipe(startX, startY, endX, endY, durationMs);

// Text input
controlChannel.SendText("Hello");
```

## Best Practices

### 1. Error Handling
- Always wrap async operations in try-catch
- Log errors with context
- Provide fallback behavior

### 2. Resource Management
- Dispose WebRTC connections properly
- Cancel CancellationTokenSource on destroy
- Clean up event handlers

### 3. Configuration
- Use PlayerPrefs for user settings
- Provide sensible defaults
- Allow runtime configuration

### 4. Logging
- Use consistent log prefixes
- Include context in log messages
- Log at appropriate levels (Info, Warning, Error)
