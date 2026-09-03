# Lessons Learned

## 2026-06-24: Settings UI Not Visible in VR

### Problem
Settings UI created with WorldSpace Canvas is not visible in Quest 3 VR.

### Cause
1. Canvas position was relative to QuestWebRtcReceiver, not camera
2. Canvas was too small or positioned behind user
3. No XR Origin setup for proper VR rendering

### Fix
1. Position Canvas relative to main camera
2. Update Canvas position in Update() to follow camera
3. Use proper WorldSpace Canvas settings

### Validation
- Check logs for Canvas position
- Verify camera.main is not null
- Test with different Canvas sizes

### References
- Unity XR Canvas documentation
- Meta Quest VR best practices

### How to avoid next time
1. Always test VR UI with camera-relative positioning
2. Use XR Origin for proper VR setup
3. Add debug logs for Canvas position

---

## 2026-06-24: ADB Connection Issues

### Problem
Wireless ADB connection times out or disconnects.

### Cause
1. Quest goes to sleep
2. Network issues
3. ADB server restart

### Fix
1. Use USB connection for reliable debugging
2. Keep Quest awake during development
3. Restart ADB server when needed

### Validation
- Check `adb devices` output
- Verify Quest is not sleeping

### References
- ADB documentation
- Quest developer guides

### How to avoid next time
1. Use USB for critical debugging
2. Keep Quest charged and awake
3. Document connection issues

---

## 2026-06-24: Unity Build Cache Issues

### Problem
APK not reflecting code changes after build.

### Cause
Unity build cache not cleared properly.

### Fix
1. Delete Library folder
2. Clean build
3. Reinstall APK

### Validation
- Check build log for source compilation
- Verify APK size changes

### References
- Unity build documentation

### How to avoid next time
1. Always clean build when changes don't appear
2. Check build logs for compilation
3. Verify APK installation

---

## 2026-06-24: Controller Input Mapping

### Problem
Quest 3 controller buttons not mapping correctly in Unity.

### Cause
1. Unity Input System configuration
2. Quest-specific button mappings
3. Missing XR Interaction Toolkit setup

### Fix
1. Use InputActionProperty for button mapping
2. Test with different KeyCode values
3. Add debug logging for input

### Validation
- Check logs for button presses
- Test all controller buttons

### References
- Unity Input System documentation
- Meta Quest controller mapping

### How to avoid next time
1. Test controller input early
2. Use Input System instead of legacy Input
3. Document button mappings

---

## 2026-06-24: WebRTC Connection Failure

### Problem
WebSocket connection to signaling server fails.

### Cause
1. Wrong IP address in code
2. Signaling server not running
3. Network issues

### Fix
1. Update IP address in source
2. Verify signaling server is running
3. Check network connectivity

### Validation
- Check logs for connection errors
- Test with netcat/curl

### References
- WebSocket documentation
- Network debugging guides

### How to avoid next time
1. Use environment variables for configuration
2. Add connection retry logic
3. Log connection details
