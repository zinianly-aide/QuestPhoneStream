# Quest CI recovery and same-build acceptance matrix

This is an evidence gate, not a list of features presumed working.
Do not merge PR #4 or PR #9 while a required row is NOT RUN or FAIL.

## CI recovery

- Unity project authored version: `2022.3.62f3c1` (Unity China). GameCI normalizes the ephemeral checkout to upstream `2022.3.62f3`; the authored asset is not rewritten in the source branch.
- `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` are required for a Unity Personal license; alternatively the documented Pro serial method applies only to licensed Pro accounts.
- A secret being present does **not** mean its license is valid. A GameCI `No ULF license found` / `TimeStamp validation failed` during activation is an environment/license blocker until the authorization is renewed and the test job actually executes.
- Renew the Unity license through an authorized Unity account/Hub, then replace the affected GitHub Actions secret without exposing the `.ulf` file, passwords, or serial in issues, logs, or the repository. Do not disable tests or bypass Unity activation.
- Proposed workflow gate: GameCI must succeed **and** an NUnit/JUnit XML report must prove that at least one test executed with zero failures. The included `scripts/ci/verify_unity_test_results.py` verifier is ready, but it is **not yet wired into GitHub Actions**, because the current GitHub credential lacks permission to modify workflow files. A Unity compile/build that never starts tests is **NOT RUN**.
- After the workflow change is authorized, the APK artifact must include `quest-apk-manifest.txt` with commit SHA, workflow run ID, Unity version, and checksum. Retain the identical APK for all Quest acceptance rows.

## Acceptance matrix

Record result as `PASS`, `FAIL`, or `NOT RUN` along with evidence (logs/screen recording), tester, firmware versions and timestamp. Repeat failed cases against the **same APK** unless a new source commit requires a fresh baseline.

| Gate | Device / test | Minimum acceptance evidence | Result |
| --- | --- | --- | --- |
| CI-01 | Unity Test Runner | XML path, total/executed/passed/failed/skipped and GitHub run URL | NOT RUN |
| CI-02 | Quest APK provenance | Source SHA, workflow run ID, APK SHA256 and download location | NOT RUN |
| Q-01 | Quest 3S + Android | Initial pairing, connected video and peer lifecycle log | NOT RUN |
| Q-02 | Quest 3S + Android | Long session duration, no unhandled disconnect, device logs | NOT RUN |
| Q-03 | Quest 3S + Android | Network interruption and restoration, recovery time and fresh peer verification | NOT RUN |
| Q-04 | Quest 3S + Android | Repeated connect/disconnect cycles with stable active device and no stale media callbacks | NOT RUN |
| Q-05 | Quest 3S + Android | Portrait/landscape switches and undistorted screen proportions | NOT RUN |
| Q-06 | Quest 3S + Android | Quest pointer tap, swipe/drag and on-device response | NOT RUN |
| Q-07 | Quest 3S + Android | Text/keyboard submission and correct input focus | NOT RUN |
| Q-08 | Quest 3S + Android | Pairing, token renewal/expiration and rejection of unauthorized operations | NOT RUN |
| Q-09 | Quest 3S local media | Flat playback, pause/seek and source capability gates | NOT RUN |
| Q-10 | Quest 3S local media | VR180/VR360, SBS LR/RL, orientation changes and no forced exit | NOT RUN |
| M-01 | Mac sender + Quest 3S | Mac screen visible, frame aspect/landscape orientation and audio/video state | NOT RUN |
| M-02 | Mac sender + Quest 3S | LingBot playback, reconnection and stable media lifecycle | NOT RUN |

**Release rule:** a green source-contract check alone never authorizes merging or labeling device behaviors verified. Keep PR #4/#9 open until every relevant gate has recorded device evidence.
