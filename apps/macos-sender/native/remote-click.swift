import Foundation
import ApplicationServices

// Separate macOS helper with its OWN Accessibility consent. Never inject any
// event if the user has not granted this helper permission in System Settings.
// Syntax: remote-click status [--prompt] | remote-click click X Y
let args = CommandLine.arguments
let prompt = args.count == 3 && args[1] == "status" && args[2] == "--prompt"
if prompt {
    let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
    _ = AXIsProcessTrustedWithOptions(options)
}
guard AXIsProcessTrusted() else {
    fputs("Accessibility permission not granted to QuestPhoneStream click helper\n", stderr)
    exit(3)
}
if args.count >= 2 && args[1] == "status" {
    print("Accessibility granted")
    exit(0)
}
// Wheel scroll uses the real cursor target (not the last place clicked).
// Positive wheel1 = scroll up, negative wheel1 = scroll down. Both axes
// use native line units; wheel deltas are bounded by the TS protocol.
if args.count == 6 && args[1] == "scroll" {
    guard let x = Double(args[2]), let y = Double(args[3]),
          let dy = Int32(args[4]), let dx = Int32(args[5]),
          x.isFinite, y.isFinite, abs(x) <= 32768, abs(y) <= 32768,
          (-4...4).contains(dy), (-4...4).contains(dx),
          (dy != 0 || dx != 0) else {
        fputs("Invalid scroll event\n", stderr)
        exit(2)
    }
    let point = CGPoint(x: x, y: y)
    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved,
            mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    guard let wheel = CGEvent(scrollWheelEvent2Source: nil, units: .line,
                            wheelCount: 2, wheel1: dy, wheel2: dx, wheel3: 0) else { exit(4) }
    wheel.post(tap: .cghidEventTap)
    exit(0)
}

// Both click and drag use the same macOS Accessibility permission gate.
let isClick = args.count == 4 && args[1] == "click"
let isDrag = args.count == 7 && args[1] == "drag"
guard isClick || isDrag,
      let x = Double(args[2]), let y = Double(args[3]),
      x.isFinite, y.isFinite, abs(x) <= 32768, abs(y) <= 32768 else {
    fputs("Invalid mouse event\n", stderr)
    exit(2)
}
let start = CGPoint(x: x, y: y)
guard let down = CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown,
                         mouseCursorPosition: start, mouseButton: .left) else { exit(4) }
down.post(tap: .cghidEventTap)
if isDrag {
    guard let endX = Double(args[4]), let endY = Double(args[5]),
          let ms = Int(args[6]), (50...3000).contains(ms),
          endX.isFinite, endY.isFinite, abs(endX) <= 32768, abs(endY) <= 32768 else {
        // Release after validation failure so the button is never stuck down.
        CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp,
                mouseCursorPosition: start, mouseButton: .left)?.post(tap: .cghidEventTap)
        exit(2)
    }
    let steps = max(2, min(45, ms / 20))
    for i in 1...steps {
        let f = CGFloat(i) / CGFloat(steps)
        let p = CGPoint(x: start.x + (endX - x) * f,
                        y: start.y + (endY - y) * f)
        CGEvent(mouseEventSource: nil, mouseType: .leftMouseDragged,
                mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
        usleep(useconds_t(ms * 1000 / steps))
    }
} else {
    usleep(30000)
}
let end = isDrag ? CGPoint(x: Double(args[4])!, y: Double(args[5])!) : start
guard let up = CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp,
                       mouseCursorPosition: end, mouseButton: .left) else { exit(4) }
up.post(tap: .cghidEventTap)
