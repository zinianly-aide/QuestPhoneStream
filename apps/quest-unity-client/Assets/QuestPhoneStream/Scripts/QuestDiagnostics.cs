using System;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace QuestPhoneStream
{
    [Serializable]
    public sealed class QuestDiagnosticsSnapshot
    {
        public string questDeviceId;
        public string selectedAndroidDeviceId;
        public string spatialVersion;
        public string nsdState;
        public string selectedDevice;
        public string serviceType;
        public string capabilities;
        public string streamId;
        public string signalingEndpoint;
        public string signalingState;
        public string sessionId;
        public string negotiationId;
        public string peerState;
        public string controlState;
        public string mediaHttpState;
        public bool videoFrameReceived;
        public int videoWidth;
        public int videoHeight;
        public string videoSource;
        public string projection;
        public int fov;
        public string stereo;
        public string eyeOrder;
        public string rendererBackend;
        public bool xrActive;
        public bool hmdTracking;
        public bool hmdPoseAvailable;
        public CapabilityRuntimeState[] capabilitiesState;

        // P2 data-plane + vision diagnostics.
        public float poseStreamHz;
        public bool spatialFastOpen;
        public bool spatialReliableOpen;
        public int spatialDroppedFrames;
        public long spatialLastSequence;
        public int reliableDroppedFrames;
        public long reliableLastSequence;
        public string cameraState;
        public string aiState;
        public long aiLatencyMs;
        public string handState;

        // P3 diagnostics. These are observations only; HUD never activates providers.
        public int anchorCount;
        public int anchorSubscribers;
        public string anchorState;
        public string depthState;
        public int depthSubscribers;
        public string interactionState;
        public int interactionSubscribers;
        public string sixDofState;
        public string gaussianState;
        public int gaussianSplatCount;
        public long gaussianLoadMs;
        public string gaussianError;

        public static QuestDiagnosticsSnapshot Capture(QuestWebRtcReceiver receiver)
        {
            var signaling = receiver == null ? null : receiver.signaling;
            var selected = receiver == null ? null : receiver.SelectedMediaDevice;
            var playback = receiver == null ? null : receiver.mediaPlayback;
            var profile = playback == null ? MediaVideoProfile.Default : playback.Profile;
            var control = receiver == null ? null : receiver.controlChannel;
            var mediaAvailable = selected != null ? selected.HasCapability("media") : receiver != null && receiver.HasMediaUrl;
            var mediaAuthorized = receiver != null && (receiver.IsMediaReady || receiver.IsMediaStale);
            var mediaActive = playback != null && playback.IsMediaMode;
            var screenAdvertised = selected != null && selected.HasCapability("screen");
            var controlAdvertised = selected != null && selected.HasCapability("control");
            var peerConnected = receiver != null && receiver.IsPeerConnected;
            var frameReceived = receiver != null && receiver.HasVideoFrame;
            var controlOpen = receiver != null && receiver.IsControlConnected;

            var telemetry = receiver == null ? null : receiver.GetComponent<SpatialTelemetryService>();
            var dataPlane = receiver == null ? null : receiver.GetComponent<SpatialDataPlaneHub>();
            var vision = receiver == null ? null : receiver.GetComponent<QuestVisionService>();
            var ai = receiver == null ? null : receiver.GetComponent<QuestAiClient>();
            var hands = receiver == null ? null : receiver.GetComponent<SpatialHandTrackingService>();
            var anchors = receiver == null ? null : receiver.GetComponent<SpatialAnchorService>();
            var depth = receiver == null ? null : receiver.GetComponent<QuestEnvironmentDepthService>();
            var interaction = receiver == null ? null : receiver.GetComponent<SpatialObjectInteractionService>();
            var sixDof = receiver == null ? null : receiver.GetComponent<SixDofMediaService>();
            var gaussian = receiver == null ? null : receiver.GetComponent<GaussianSplatPocRenderer>();

            var snapshot = new QuestDiagnosticsSnapshot {
                questDeviceId = signaling == null ? string.Empty : signaling.questDeviceId,
                selectedAndroidDeviceId = receiver == null ? string.Empty : receiver.SelectedAndroidDeviceId,
                spatialVersion = "1",
                nsdState = receiver == null || receiver.mediaDiscovery == null ? "Unavailable" : receiver.mediaDiscovery.State,
                selectedDevice = selected == null ? "None" : selected.name,
                serviceType = selected == null ? string.Empty : selected.serviceType,
                capabilities = selected == null ? string.Empty : selected.capabilities,
                streamId = selected == null ? string.Empty : selected.streamId,
                signalingEndpoint = selected == null ? string.Empty : selected.signalingUrl,
                signalingState = signaling == null ? "Unavailable" : signaling.State.ToString(),
                sessionId = signaling == null ? string.Empty : signaling.ActiveSessionId,
                negotiationId = signaling == null ? string.Empty : signaling.NegotiationId,
                peerState = receiver == null ? "Unavailable" : receiver.PeerConnectionState,
                controlState = control == null ? "Unavailable" : control.StateLabel,
                mediaHttpState = receiver == null || !receiver.HasMediaUrl ? "Not configured" :
                    receiver.IsMediaReady ? "Ready" : receiver.IsMediaFailed ? "Unreachable" : "Checking",
                videoFrameReceived = frameReceived,
                videoWidth = receiver == null ? 0 : receiver.VideoWidth,
                videoHeight = receiver == null ? 0 : receiver.VideoHeight,
                videoSource = receiver == null ? "None" : receiver.VideoSource,
                projection = profile.projection.ToString(),
                fov = profile.fov,
                stereo = profile.stereo.ToString(),
                eyeOrder = profile.eyeOrder.ToString(),
                rendererBackend = playback == null || playback.vrRenderer == null ? "None" : playback.vrRenderer.vrBackend.ToString(),
                xrActive = XRSettings.enabled && XRSettings.isDeviceActive,
                hmdTracking = false,
                hmdPoseAvailable = false,
                capabilitiesState = CapabilityRuntimeStateFactory.ForQuest(
                    screenAdvertised, controlAdvertised, mediaAvailable, mediaAuthorized, mediaActive,
                    peerConnected, frameReceived, controlOpen),

                poseStreamHz = telemetry == null ? 0f : telemetry.PoseStreamHz,
                spatialFastOpen = dataPlane != null && dataPlane.IsFastOpen,
                spatialReliableOpen = dataPlane != null && dataPlane.IsReliableOpen,
                spatialDroppedFrames = dataPlane == null ? 0 : dataPlane.DroppedFrames,
                spatialLastSequence = dataPlane == null ? -1 : dataPlane.LastSequence,
                reliableDroppedFrames = dataPlane == null ? 0 : dataPlane.ReliableDroppedFrames,
                reliableLastSequence = dataPlane == null ? -1 : dataPlane.ReliableLastSequence,
                cameraState = vision == null ? "Unavailable" : vision.CameraState,
                aiState = ai == null ? "Unavailable" : ai.IsRequestActive ? "Requesting" : ai.CanRequest ? "Ready" : "Not configured",
                aiLatencyMs = ai == null ? 0 : ai.LastLatencyMs,
                handState = hands == null ? "Unavailable" : hands.HandTrackingState,

                anchorCount = anchors == null ? 0 : anchors.AnchorCount,
                anchorSubscribers = anchors == null ? 0 : anchors.SubscriberCount,
                anchorState = anchors == null ? "Unavailable" : anchors.StateText,
                depthState = depth == null ? "Unavailable" : depth.DepthState,
                depthSubscribers = depth == null ? 0 : depth.SubscriberCount,
                interactionState = interaction == null ? "Unavailable" : interaction.InteractionState,
                interactionSubscribers = interaction == null ? 0 : interaction.SubscriberCount,
                sixDofState = sixDof == null ? "Unavailable · external provider required" : sixDof.StateText,
                gaussianState = gaussian == null ? "Unavailable" : gaussian.StateText,
                gaussianSplatCount = gaussian == null ? 0 : gaussian.SplatCount,
                gaussianLoadMs = gaussian == null ? 0 : gaussian.LastLoadMs,
                gaussianError = gaussian == null ? null : gaussian.LastError
            };

            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.isValid)
            {
                head.TryGetFeatureValue(CommonUsages.isTracked, out snapshot.hmdTracking);
                Vector3 position;
                Quaternion rotation;
                snapshot.hmdPoseAvailable = head.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                    head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            }
            return snapshot;
        }

        public string ToDisplayText()
        {
            var text = new StringBuilder(2600);
            AddSection(text, "Device", $"Quest deviceId: {questDeviceId}\nSelected Android: {selectedAndroidDeviceId}\nSpatial: v{spatialVersion}");
            AddSection(text, "Discovery", $"NSD: {nsdState}\nSelected: {selectedDevice}\nService: {serviceType}\nCaps: {capabilities}\nStream: {streamId}\nSignaling: {signalingEndpoint}");
            AddSection(text, "Network", $"Signaling: {signalingState}\nSession: {sessionId}\nNegotiation: {negotiationId}\nWebRTC peer: {peerState}\nControl DataChannel: {controlState}\nMedia HTTP: {mediaHttpState}");
            AddSection(text, "Video", $"Frame received: {videoFrameReceived}\nResolution: {videoWidth}x{videoHeight}\nSource: {videoSource}\nProfile: {projection} / {fov} / {stereo} / {eyeOrder}\nRenderer: {rendererBackend}");
            AddSection(text, "XR", $"OpenXR active: {xrActive}\nHMD tracking: {hmdTracking}\nHMD pose: {hmdPoseAvailable}\nHands: {handState}");
            AddSection(text, "Spatial Data Plane",
                $"Pose: {poseStreamHz:0} Hz\nFast channel: {(spatialFastOpen ? "open" : "closed")} · dropped={spatialDroppedFrames} · lastSeq={spatialLastSequence}\n" +
                $"Reliable channel: {(spatialReliableOpen ? "open" : "closed")} · dropped={reliableDroppedFrames} · lastSeq={reliableLastSequence}");
            AddSection(text, "Vision / AI", $"Camera: {cameraState}\nAI: {aiState}\nAI latency: {aiLatencyMs} ms");
            AddSection(text, "P3",
                $"Anchors: {anchorState} (count={anchorCount}, subscribers={anchorSubscribers})\n" +
                $"Depth: {depthState} (subscribers={depthSubscribers})\n" +
                $"Interaction: {interactionState} (subscribers={interactionSubscribers})\n" +
                $"6DoF: {sixDofState}\n" +
                $"3DGS: {gaussianState} · splats={gaussianSplatCount} · load={gaussianLoadMs} ms" +
                (string.IsNullOrEmpty(gaussianError) ? string.Empty : $"\n3DGS error: {gaussianError}"));
            text.AppendLine("Capabilities");
            if (capabilitiesState != null)
                foreach (var capability in capabilitiesState)
                    text.AppendLine($"{capability.name}: available={capability.available} authorized={capability.authorized} active={capability.active} transport={capability.transport}");
            return text.ToString();
        }

        private static void AddSection(StringBuilder text, string title, string body)
        {
            text.AppendLine(title);
            text.AppendLine(body);
            text.AppendLine();
        }
    }

    public sealed class QuestDiagnostics : MonoBehaviour
    {
        public QuestWebRtcReceiver receiver;
        public void Initialize(QuestWebRtcReceiver value) { receiver = value; }
        public QuestDiagnosticsSnapshot CaptureSnapshot() => QuestDiagnosticsSnapshot.Capture(receiver);
    }
}
