export type SignalingState = 'idle' | 'connecting' | 'connected' | 'failed' | 'closed';

export interface SignalingConfig {
  url: string;
  token: string;
  role: 'android' | 'quest';
  deviceId: string;
}

export interface SignalingEvents {
  onStateChange: (state: SignalingState) => void;
  onRegistered: (deviceId: string) => void;
  onSessionCreated: (sessionId: string, androidDeviceId: string, questDeviceId: string) => void;
  onOffer: (sdp: string) => void;
  onIceCandidate: (candidate: RTCIceCandidateInit) => void;
  onError: (message: string) => void;
  onLog: (message: string) => void;
}

export class SignalingClient {
  private ws: WebSocket | null = null;
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null;
  private state: SignalingState = 'idle';

  constructor(
    private config: SignalingConfig,
    private events: SignalingEvents,
  ) {}

  getState() { return this.state; }

  connect() {
    this.setState('connecting');
    this.log(`ws connecting to ${this.config.url} ...`);

    try {
      this.ws = new WebSocket(this.config.url);
    } catch (err) {
      this.setState('failed');
      this.log(`ws connection error: ${err}`);
      return;
    }

    this.ws.onopen = () => {
      this.setState('connected');
      this.log('ws connected ✓');
      this.sendRegister();
      this.startHeartbeat();
    };

    this.ws.onmessage = (ev) => {
      try {
        this.handleMessage(JSON.parse(ev.data as string));
      } catch (err) {
        this.log(`parse error: ${err}`);
      }
    };

    this.ws.onerror = () => {
      this.log('ws error');
    };

    this.ws.onclose = (ev) => {
      this.log(`ws closed: code=${ev.code} reason=${ev.reason}`);
      this.stopHeartbeat();
      if (this.state !== 'failed') {
        this.setState('closed');
      }
    };
  }

  createSession(sessionId: string, androidDeviceId: string, questDeviceId: string) {
    this.send({
      type: 'create_session',
      token: this.config.token,
      sessionId,
      androidDeviceId,
      questDeviceId,
    });
    this.log(`create_session sent: ${sessionId}`);
  }

  sendAnswer(sessionId: string, from: string, to: string, sdp: string) {
    this.send({
      type: 'answer',
      token: this.config.token,
      sessionId,
      from,
      to,
      sdp,
    });
    this.log('answer sent');
  }

  sendIce(sessionId: string, from: string, to: string, candidate: RTCIceCandidateInit) {
    this.send({
      type: 'ice',
      token: this.config.token,
      sessionId,
      from,
      to,
      candidate,
    });
    this.log('ice sent');
  }

  close() {
    this.stopHeartbeat();
    this.ws?.close(1000, 'manual close');
    this.ws = null;
    this.setState('closed');
    this.log('ws closed by client');
  }

  private handleMessage(msg: Record<string, unknown>) {
    switch (msg.type) {
      case 'registered':
        this.log(`registered as ${msg.deviceId}`);
        this.events.onRegistered(msg.deviceId as string);
        break;
      case 'session_created':
        this.log(`session_created: ${msg.sessionId}`);
        this.events.onSessionCreated(
          msg.sessionId as string,
          msg.androidDeviceId as string,
          msg.questDeviceId as string,
        );
        break;
      case 'offer':
        this.log('offer received');
        this.events.onOffer(msg.sdp as string);
        break;
      case 'ice': {
        const c = msg.candidate as Record<string, unknown>;
        this.events.onIceCandidate({
          candidate: c.candidate as string,
          sdpMid: (c.sdpMid as string) ?? null,
          sdpMLineIndex: (c.sdpMLineIndex as number) ?? 0,
        });
        this.log('ice candidate received');
        break;
      }
      case 'error':
        this.log(`server error: ${msg.code} ${msg.message}`);
        this.events.onError(`${msg.code}: ${msg.message}`);
        break;
      case 'peer_unavailable':
        this.log(`peer unavailable: ${msg.deviceId}`);
        break;
    }
  }

  private sendRegister() {
    this.send({
      type: 'register',
      token: this.config.token,
      role: this.config.role,
      deviceId: this.config.deviceId,
    });
    this.log('register sent');
  }

  private send(obj: Record<string, unknown>) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(obj));
    }
  }

  private startHeartbeat() {
    this.stopHeartbeat();
    this.heartbeatTimer = setInterval(() => {
      this.send({
        type: 'heartbeat',
        token: this.config.token,
        deviceId: this.config.deviceId,
        timestamp: Date.now(),
      });
    }, 15000);
  }

  private stopHeartbeat() {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
  }

  private setState(s: SignalingState) {
    this.state = s;
    this.events.onStateChange(s);
  }

  private log(message: string) {
    const time = new Date().toLocaleTimeString('en-GB', { hour12: false });
    this.events.onLog(`[${time}] ${message}`);
  }
}
