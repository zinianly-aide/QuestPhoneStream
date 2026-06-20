import { useCallback, useEffect, useRef, useState } from 'react';
import { SignalingClient, type SignalingState } from './signaling';
import type { SignalingConfig } from './signaling';

const DEFAULT_CONFIG: SignalingConfig = {
  url: 'ws://192.168.1.10:8787',
  token: 'dev-token',
  role: 'quest',
  deviceId: 'quest-3s-001',
};

const SESSION_ID = 'local-session-001';
const ANDROID_DEVICE_ID = 'android-phone-001';

interface VideoStats {
  width: number;
  height: number;
  fps: number;
}

export function App() {
  const [config, setConfig] = useState<SignalingConfig>(DEFAULT_CONFIG);
  const [signalingState, setSignalingState] = useState<SignalingState>('idle');
  const [iceState, setIceState] = useState<RTCIceConnectionState>('new');
  const [pcState, setPcState] = useState<RTCSignalingState>('stable');
  const [videoStats, setVideoStats] = useState<VideoStats>({ width: 0, height: 0, fps: 0 });
  const [logs, setLogs] = useState<string[]>([]);
  const [connected, setConnected] = useState(false);

  const videoRef = useRef<HTMLVideoElement>(null);
  const pcRef = useRef<RTCPeerConnection | null>(null);
  const signalingRef = useRef<SignalingClient | null>(null);
  const statsIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const prevFrameCountRef = useRef(0);

  const addLog = useCallback((msg: string) => {
    setLogs(prev => [...prev.slice(-49), msg]);
  }, []);

  // Poll video stats
  useEffect(() => {
    if (!connected) return;
    statsIntervalRef.current = setInterval(async () => {
      const pc = pcRef.current;
      if (!pc) return;
      const receivers = pc.getReceivers();
      for (const r of receivers) {
        if (r.track?.kind !== 'video') continue;
        const stats = await r.getStats();
        let frameCount = 0;
        let width = 0;
        let height = 0;
        stats.forEach((report) => {
          if (report.type === 'inbound-rtp' && report.kind === 'video') {
            frameCount = report.framesReceived ?? 0;
            width = report.frameWidth ?? 0;
            height = report.frameHeight ?? 0;
          }
        });
        const delta = frameCount - prevFrameCountRef.current;
        prevFrameCountRef.current = frameCount;
        setVideoStats({ width, height, fps: delta > 0 ? delta : 0 });
      }
    }, 1000);
    return () => {
      if (statsIntervalRef.current) clearInterval(statsIntervalRef.current);
    };
  }, [connected]);

  const cleanup = useCallback(() => {
    if (statsIntervalRef.current) clearInterval(statsIntervalRef.current);
    pcRef.current?.close();
    pcRef.current = null;
    signalingRef.current?.close();
    signalingRef.current = null;
    setConnected(false);
    setIceState('new');
    setPcState('stable');
    setVideoStats({ width: 0, height: 0, fps: 0 });
    prevFrameCountRef.current = 0;
  }, []);

  const handleConnect = useCallback(() => {
    if (connected) {
      cleanup();
      addLog('Disconnected');
      return;
    }

    // Create PeerConnection
    const pc = new RTCPeerConnection({
      iceServers: [{ urls: 'stun:stun.l.google.com:19302' }],
    });
    pcRef.current = pc;

    pc.onicecandidate = (ev) => {
      if (ev.candidate && signalingRef.current) {
        signalingRef.current.sendIce(
          SESSION_ID,
          config.deviceId,
          ANDROID_DEVICE_ID,
          ev.candidate.toJSON(),
        );
      }
    };

    pc.oniceconnectionstatechange = () => {
      setIceState(pc.iceConnectionState);
      addLog(`ICE state: ${pc.iceConnectionState}`);
    };

    pc.onsignalingstatechange = () => {
      setPcState(pc.signalingState);
    };

    pc.ontrack = (ev) => {
      addLog(`track received: ${ev.track.kind}`);
      if (videoRef.current && ev.streams[0]) {
        videoRef.current.srcObject = ev.streams[0];
        videoRef.current.play().catch(() => {});
      }
    };

    // Create SignalingClient
    const signaling = new SignalingClient(config, {
      onStateChange: setSignalingState,
      onRegistered: (deviceId) => {
        addLog(`Registered as ${deviceId}, creating session ...`);
        signaling.createSession(SESSION_ID, ANDROID_DEVICE_ID, config.deviceId);
      },
      onSessionCreated: (sid, _androidId, _questId) => {
        addLog(`Session created: ${sid}, waiting for Android offer ...`);
      },
      onOffer: async (sdp) => {
        addLog('Setting remote offer ...');
        await pc.setRemoteDescription(new RTCSessionDescription({ type: 'offer', sdp }));
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        if (signaling.getState() === 'connected') {
          signaling.sendAnswer(SESSION_ID, config.deviceId, ANDROID_DEVICE_ID, answer.sdp!);
        }
      },
      onIceCandidate: (candidate) => {
        pc.addIceCandidate(new RTCIceCandidate(candidate)).catch((err) => {
          addLog(`addIceCandidate error: ${err}`);
        });
      },
      onError: (msg) => addLog(`ERROR: ${msg}`),
      onLog: addLog,
    });

    signalingRef.current = signaling;
    signaling.connect();
    setConnected(true);
  }, [config, connected, cleanup, addLog]);

  const urlMode = config.url.startsWith('wss://')
    ? { label: '🔒 TLS/WSS 模式', color: '#4CAF50' }
    : config.url.startsWith('ws://')
      ? { label: '🔓 局域网明文调试模式', color: '#FF9800' }
      : { label: '⚠ URL 格式异常', color: '#F44336' };

  return (
    <div style={styles.container}>
      {/* Header */}
      <div style={styles.header}>
        <h1 style={styles.title}>Quest Phone Stream Viewer</h1>
        <span style={{ ...styles.badge, backgroundColor: urlMode.color }}>{urlMode.label}</span>
      </div>

      {/* Video */}
      <div style={styles.videoContainer}>
        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          style={styles.video}
        />
        {!connected && (
          <div style={styles.videoOverlay}>
            等待连接 ...
          </div>
        )}
        {connected && videoStats.width > 0 && (
          <div style={styles.videoInfo}>
            {videoStats.width}×{videoStats.height} @ {videoStats.fps}fps
          </div>
        )}
      </div>

      {/* Status */}
      <div style={styles.card}>
        <div style={styles.cardTitle}>STATUS</div>
        <StatusRow label="Signaling" value={signalingState} color={stateColor(signalingState)} />
        <StatusRow label="ICE" value={iceState} color={iceColor(iceState)} />
        <StatusRow label="WebRTC" value={pcState} color={pcState === 'stable' ? '#4CAF50' : '#FF9800'} />
        <StatusRow label="Resolution" value={videoStats.width > 0 ? `${videoStats.width}×${videoStats.height}` : '—'} color="#e0e0e0" />
        <StatusRow label="FPS" value={videoStats.fps > 0 ? `${videoStats.fps}` : '—'} color="#e0e0e0" />
      </div>

      {/* Config */}
      <div style={styles.card}>
        <div style={styles.cardTitle}>CONFIGURATION</div>
        <ConfigField label="Signaling URL" value={config.url} onChange={v => setConfig(c => ({ ...c, url: v }))} />
        <ConfigField label="Token" value={config.token} onChange={v => setConfig(c => ({ ...c, token: v }))} password />
        <ConfigField label="Quest Device ID" value={config.deviceId} onChange={v => setConfig(c => ({ ...c, deviceId: v }))} />
        <div style={styles.staticField}>
          <span style={styles.fieldLabel}>Session ID</span>
          <span style={styles.fieldValue}>{SESSION_ID}</span>
        </div>
        <div style={styles.staticField}>
          <span style={styles.fieldLabel}>Android Device ID</span>
          <span style={styles.fieldValue}>{ANDROID_DEVICE_ID}</span>
        </div>
      </div>

      {/* Actions */}
      <div style={styles.card}>
        <div style={styles.cardTitle}>ACTIONS</div>
        <button
          style={{ ...styles.btn, backgroundColor: connected ? '#C62828' : '#2E7D32' }}
          onClick={handleConnect}
        >
          {connected ? '⏹ Disconnect' : '▶ Connect'}
        </button>
      </div>

      {/* Log */}
      <div style={styles.card}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={styles.cardTitle}>LOG</div>
          <button style={styles.clearBtn} onClick={() => setLogs([])}>Clear</button>
        </div>
        <div style={styles.logBox}>
          {logs.map((log, i) => (
            <div key={i} style={styles.logLine}>{log}</div>
          ))}
        </div>
      </div>
    </div>
  );
}

function StatusRow({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div style={styles.statusRow}>
      <span style={styles.statusLabel}>{label}</span>
      <span style={{ ...styles.statusValue, color }}>{value}</span>
    </div>
  );
}

function ConfigField({ label, value, onChange, password }: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  password?: boolean;
}) {
  return (
    <div style={{ marginBottom: 8 }}>
      <label style={styles.fieldLabel}>{label}</label>
      <input
        type={password ? 'password' : 'text'}
        value={value}
        onChange={e => onChange(e.target.value)}
        style={styles.input}
      />
    </div>
  );
}

function stateColor(s: SignalingState): string {
  switch (s) {
    case 'connected': return '#4CAF50';
    case 'connecting': return '#FF9800';
    case 'failed': return '#F44336';
    case 'closed': return '#757575';
    default: return '#757575';
  }
}

function iceColor(s: RTCIceConnectionState): string {
  switch (s) {
    case 'connected':
    case 'completed': return '#4CAF50';
    case 'checking': return '#FF9800';
    case 'failed': return '#F44336';
    case 'disconnected': return '#FF5722';
    default: return '#757575';
  }
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    maxWidth: 640,
    margin: '0 auto',
    padding: 16,
    minHeight: '100vh',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
    flexWrap: 'wrap',
    gap: 8,
  },
  title: {
    fontSize: 20,
    fontWeight: 700,
    color: '#90CAF9',
    margin: 0,
  },
  badge: {
    fontSize: 12,
    fontWeight: 600,
    padding: '4px 12px',
    borderRadius: 12,
    color: '#fff',
  },
  videoContainer: {
    position: 'relative',
    backgroundColor: '#000',
    borderRadius: 8,
    overflow: 'hidden',
    marginBottom: 16,
    aspectRatio: '16/9',
  },
  video: {
    width: '100%',
    height: '100%',
    objectFit: 'contain',
  },
  videoOverlay: {
    position: 'absolute',
    inset: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    color: '#757575',
    fontSize: 16,
  },
  videoInfo: {
    position: 'absolute',
    top: 8,
    right: 8,
    backgroundColor: 'rgba(0,0,0,0.7)',
    color: '#B2FF59',
    fontSize: 12,
    fontFamily: 'monospace',
    padding: '2px 8px',
    borderRadius: 4,
  },
  card: {
    backgroundColor: '#1a1a1a',
    borderRadius: 8,
    padding: 16,
    marginBottom: 12,
  },
  cardTitle: {
    fontSize: 12,
    fontWeight: 700,
    color: '#90CAF9',
    letterSpacing: 1,
    marginBottom: 8,
  },
  statusRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: '4px 0',
  },
  statusLabel: {
    fontWeight: 600,
    fontSize: 14,
    color: '#aaa',
  },
  statusValue: {
    fontSize: 14,
    fontWeight: 500,
    fontFamily: 'monospace',
  },
  staticField: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: '4px 0',
  },
  fieldLabel: {
    fontSize: 12,
    color: '#888',
    display: 'block',
    marginBottom: 4,
  },
  fieldValue: {
    fontSize: 14,
    color: '#e0e0e0',
    fontFamily: 'monospace',
  },
  input: {
    width: '100%',
    padding: '8px 12px',
    fontSize: 14,
    backgroundColor: '#2a2a2a',
    border: '1px solid #333',
    borderRadius: 6,
    color: '#e0e0e0',
    outline: 'none',
    fontFamily: 'monospace',
  },
  btn: {
    width: '100%',
    padding: '12px 16px',
    fontSize: 16,
    fontWeight: 600,
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    cursor: 'pointer',
    marginTop: 4,
  },
  logBox: {
    backgroundColor: '#0d1117',
    borderRadius: 6,
    padding: 12,
    maxHeight: 240,
    overflowY: 'auto',
    fontFamily: 'monospace',
    fontSize: 11,
  },
  logLine: {
    color: '#B2FF59',
    lineHeight: 1.6,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-all',
  },
  clearBtn: {
    background: 'none',
    border: 'none',
    color: '#888',
    fontSize: 12,
    cursor: 'pointer',
    padding: '2px 8px',
  },
};
