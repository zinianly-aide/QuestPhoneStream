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
