# Quest 3S: click to enable wireless ADB, Mac reconnects automatically

**Development builds only.** No background boot receiver; user taps a button
after Quest cold boots. System pairing and security remain enforced.

## First-time preparation

1. Turn on Quest developer mode. Connect Quest to your trusted Mac via USB.
2. Install a QuestPhoneStream **Unity Development Build** with the developer
   settings permission in the generated Android manifest.
3. On a USB-authorized development machine, request the one-time grant:

    adb shell pm grant com.questphonestream.quest android.permission.WRITE_SECURE_SETTINGS

   Horizon OS may reject this; never treat denial as success.
4. On Quest: QuestPhoneStream > Settings > Diagnostics > Wireless ADB >
   Enable Wireless ADB. The button checks the grant and requests Android
   Settings.Global adb_wifi_enabled = 1. Android may still refuse or delay
   turning on its TLS server.
5. If permission is missing or the write is rejected, select System Settings
   on the same page and enable Wireless debugging in the system UI.
6. Open QuestPhoneStream Mac Sender. It automatically starts the official
   ADB server and observes TLS mDNS discovery at 15-second intervals.

## TLS pairing and dynamic ports

If Mac is not already TLS-paired, use the Quest system Wireless debugging page
to select Pair device with pairing code, and enter on Mac:

    adb pair QUEST_IP:PAIRING_PORT

The pairing port is DIFFERENT from the connection port. The ADB daemon
automatically discovers the dynamic _adb-tls-connect._tcp port and reconnects
to previously paired devices. No manual 5555 or fixed Quest IP needed.

Confirm with:

    adb mdns services
    adb devices -l

The Mac card may include other Android devices on the same LAN: check the
address and authenticated connection before selecting a device to debug.

## Cold boot test (not yet executed on real Quest)

- Power down and fully boot Quest; wait for trusted Wi-Fi.
- Open QuestPhoneStream and tap Enable Wireless ADB.
- Confirm Mac sees _adb-tls-connect._tcp with a dynamic port and then the
  Quest entry in adb devices -l (state: device, not unauthorized/offline).
- Repeat after standby and network changes.
- If the system refuses the request, use the built-in system settings fallback.

## Security and known limitations

The debug-only WRITE_SECURE_SETTINGS grant has broad capabilities. Grant it
only on a headset you own and control. Disable Wireless debugging when not
in use; if desired revoke over an authorized USB ADB session:

    adb shell pm revoke com.questphonestream.quest android.permission.WRITE_SECURE_SETTINGS

Release builds do not declare the grant. Android controls adbd availability,
TLS pairing and the changing port. The Mac app never executes discovered
addresses as shell commands, and never bypasses device authentication.

Mac CI tests and TypeScript build pass; Quest Unity tests, Horizon OS privilege
support, real cold-boot pairing and an installable APK remain unverified.

## Restored application mDNS presence (2026-10-23)

Historical sources:
- Android sender registers `_qps-device._tcp.` plus legacy `_qps-media._tcp.` with `MediaNsdRegistration.kt`.
- macOS sender publishes `_qps-device._tcp.` through `bonjour-service`; its Oct 14 ADB assistant observes the **OS** `_adb-tls-connect._tcp.` only after official TLS pairing.
- Older Quest Unity `MediaDeviceDiscovery` browsed these services, but did not register any Quest-owned advertisement.

Quest Unity now registers an explicit, separate `_qps-quest._tcp.` service using Android
`NsdManager` while QuestPhoneStream is open and resumed. TXT contains `v=1`, a locally
persisted random UUID, `name=QuestPhoneStream Quest`, `platform=quest`, `caps=scene`.
Its ephemeral TCP presence socket closes connections without reading payloads; it offers
**no ADB proxy, remote commands, credentials, or signaling endpoint**. The lifecycle
unregisters and closes the socket on pause/destroy. Network/multicast permissions are
added to the generated Android manifest.

The Mac sender now browses `_qps-quest._tcp.` separately and labels app presence
independently from authenticated ADB. It does not invoke `adb connect` using untrusted
broadcast ports, and still uses Android's official authenticated TLS ADB mDNS.

Verification: Unity PlayMode 161/161 and macOS sender 42/42 tests, Mac TypeScript build,
Quest 3S B2.1 upgraded via paired ADB without clearing HefuVR scene data; macOS `dns-sd`
resolved `_qps-quest._tcp.` with `platform=quest`, and the Mac Node presence watcher
received the new service. ADB OS service reappeared independently with a different
port after headset boot. mDNS requires QuestPhoneStream app to be foreground/resumed;
its presence alone cannot reconnect to Android's secure ADB service.

## Selectable Developer Diagnostics views (2026-10-23)

The Quest diagnostics UI is behind the existing `QPS_DEV_TOOLS || DEVELOPMENT_BUILD`
compile gate. **Standard `BuildAndroid` deliberately omits this developer page**.
When testing developer UI, use
`QuestPhoneStream.Editor.QuestPhoneStreamBuild.BuildAndroidDevelopmentTools`, not the
production `BuildAndroid` method; this also retains the prior Wireless ADB helper.
The HUD now offers XR-selectable read-only sections: All, Discovery, Streaming,
XR / Spatial and Caps. The selected section is saved as a local PlayerPrefs value.
Discovery shows receiver NSD, signaling/WebRTC status, Quest app-level mDNS
publication and OS wireless-debugging status separately. It is observational;
selecting a debug view never enables ADB, changes signaling, or activates providers.
