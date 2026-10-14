# Quest to Mac mouse wheel (development preview)

Interaction: Point the right Touch Plus controller's ray at the Mac Screen
panel, then push the right thumbstick up/down for wheel scroll,
left/right for horizontal wheel. The trigger continues clicking;
the existing swipe remains drag. Android agent control stays unchanged.

Transport: Existing authenticated WebRTC control channel, ControlCommandDto
version 1.0, new type scroll, frame pixel x/y, signed integer deltaX/Y
from -4 to 4, existing session/device IDs and timestamp.

Input shaping: 0.28 thumbstick dead zone, 1-3 wheel lines per tick,
85 ms send interval, only while right controller ray intersects the
currently selected Mac screen and screen input is enabled.

Mac receiver: Strict session/clock/pixel/delta validation and
60 ms rate limit; explicit local Enable Quest clicks plus Accessibility
permission required. Native helper emits a two-axis Quartz scroll event
under the remote cursor position. Existing click/swipe retained.

Tests: Mac Sender build/39 tests/native ARM64 compile passed. Unity
Quest Development APK build passed. Do not fire real mouse events in
automated tests.

Mac test app:
apps/macos-sender/release/wheel-test/QuestPhoneStream Mac.app
Quest APK:
apps/quest-unity-client/Builds/QuestPhoneStream.apk

Acceptance: Verify vertical and horizontal scroll on a scrollable Mac window;
click/drag unchanged; stick outside Mac surface does nothing; media/home
does not emit scroll; disconnect/reconnect stops unauthorized input.
The installed original Mac app is not overwritten by the test bundle.
