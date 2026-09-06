using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuestPhoneStream.Tests
{
    // Drive the real client's message handler, not a separate mock state-machine implementation.
    // Socket/server integration is exercised separately by the Node tests.
    public class SignalingTests
    {
        private GameObject _root;
        private QuestSignalingClient _client;
        private readonly List<ConnectionState> _states = new List<ConnectionState>();
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Signaling test");
            _client = _root.AddComponent<QuestSignalingClient>();
            Set("_activeQuest", "q");
            Set("_activeAndroid", "a");
            Set("_activeSession", "s");
            Set("<NegotiationId>k__BackingField", "new");
            Set("_registered", new TaskCompletionSource<bool>());
            Set("_sessionReady", new TaskCompletionSource<bool>());
            Set("_mediaReady", new TaskCompletionSource<bool>());
            _states.Clear();
            _client.StateChanged += _states.Add;
        }

        [UnityTearDown]
        public IEnumerator TearDown() { UnityEngine.Object.Destroy(_root); yield return null; }
        private void Set(string field, object value) => typeof(QuestSignalingClient).GetField(field, Private).SetValue(_client, value);
        private void State(ConnectionState value) => Set("<State>k__BackingField", value);
        private void Receive(SignalMessage message) =>
            typeof(QuestSignalingClient).GetMethod("HandleMessage", Private).Invoke(_client, new object[] { message, 0 });

        [Test]
        public void RegistrationRequiresMatchingAck()
        {
            State(ConnectionState.Registering);
            Assert.IsFalse(_states.Contains(ConnectionState.Registered));
            Receive(new SignalMessage { type = "registered", role = "quest", deviceId = "q" });
            Assert.AreEqual(ConnectionState.Registered, _client.State);
        }

        [Test]
        public void BadTokenNeverReportsRegistered()
        {
            State(ConnectionState.Registering);
            Receive(new SignalMessage { type = "error", code = "unauthorized", message = "DO_NOT_LOG_SECRET" });
            Assert.AreEqual(ConnectionState.AuthFailed, _client.State);
            Assert.IsFalse(_states.Contains(ConnectionState.Registered));
        }

        [Test]
        public void PeerConnectedIsNotMediaReceived()
        {
            State(ConnectionState.Negotiating);
            _client.ReportMediaState("new", ConnectionState.MediaConnected);
            Assert.AreEqual(ConnectionState.Negotiating, _client.State);
            _client.ReportMediaState("new", ConnectionState.PeerConnected);
            Assert.AreEqual(ConnectionState.PeerConnected, _client.State);
            _client.ReportMediaState("new", ConnectionState.MediaConnected);
            Assert.AreEqual(ConnectionState.MediaConnected, _client.State);
        }

        [Test]
        public void StaleMessagesAndMediaCallbacksCannotAffectNewSession()
        {
            State(ConnectionState.SessionRequesting);
            Receive(new SignalMessage { type = "peer_unavailable", sessionId = "s", negotiationId = "old" });
            Receive(new SignalMessage { type = "error", code = "session_replaced", sessionId = "s", negotiationId = "old" });
            _client.ReportMediaState("old", ConnectionState.MediaFailed);
            Assert.AreEqual(ConnectionState.SessionRequesting, _client.State);
            Receive(new SignalMessage { type = "session_created", sessionId = "s", negotiationId = "new", androidDeviceId = "a", questDeviceId = "q" });
            Assert.AreEqual(ConnectionState.Negotiating, _client.State);
        }

        [Test]
        public void OfflineEndsNegotiationAndInvalidatesMedia()
        {
            State(ConnectionState.SessionRequesting);
            var resets = 0;
            _client.NegotiationInvalidated += () => ++resets;
            Receive(new SignalMessage { type = "peer_unavailable", sessionId = "s", negotiationId = "new", deviceId = "a" });
            Assert.AreEqual(ConnectionState.DeviceOffline, _client.State);
            Assert.IsFalse(_client.IsCurrentNegotiation("new"));
            Assert.AreEqual(1, resets);
        }

        [Test]
        public void RegistrationWireMessageDoesNotIncludeUnionFields()
        {
            var json = SignalingWire.Serialize(new SignalMessage { type = "register", token = "test", role = "quest", deviceId = "q" });
            StringAssert.DoesNotContain("negotiationId", json);
            StringAssert.DoesNotContain("candidate", json);
            StringAssert.DoesNotContain("sessionId", json);
        }

        [Test]
        public void ConcurrentReconnectReturnsTheExistingAttempt()
        {
            var pending = new TaskCompletionSource<bool>();
            _client.questDeviceId = "q";
            _client.androidDeviceId = "a";
            _client.sessionId = "s";
            Set("<IsConnecting>k__BackingField", true);
            Set("_attempt", pending.Task);
            Set("_activeSignalingUrl", _client.signalingUrl);
            Set("_activeSession", "s");
            Set("_activeQuest", "q");
            Set("_activeAndroid", "a");
            Set("_activeToken", _client.token);
            Assert.AreSame(pending.Task, _client.ReconnectAsync());
            Assert.AreSame(pending.Task, _client.ReconnectAsync());
            pending.SetResult(false);
        }

        [UnityTest]
        public IEnumerator MissingSessionAckEndsWithSessionFailed()
        {
            State(ConnectionState.SessionRequesting);
            var pending = new TaskCompletionSource<bool>();
            var task = (Task<bool>)typeof(QuestSignalingClient).GetMethod("WaitFor", Private).Invoke(_client,
                new object[] { pending.Task, 20, ConnectionState.SessionFailed, 0, CancellationToken.None });
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.Result);
            Assert.AreEqual(ConnectionState.SessionFailed, _client.State);
        }
    }
}
