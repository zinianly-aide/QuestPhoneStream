import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("qps", {
  getConfig: () => ipcRenderer.invoke("qps:get-config"),
  controlEnable: (enabled: boolean) => ipcRenderer.invoke("qps:control-enable", enabled),
  controlClick: (payload: Record<string, unknown>) => ipcRenderer.invoke("qps:control-click", payload),
  adbStatus: () => ipcRenderer.invoke("qps:adb-status"),
  adbRefresh: () => ipcRenderer.invoke("qps:adb-refresh"),
  onAdbState: (cb: (state: Record<string, unknown>) => void) => {
    ipcRenderer.on("qps:adb-state", (_event, state) => cb(state));
  },
  listSources: () => ipcRenderer.invoke("qps:list-sources"),
  openScreenSettings: () => ipcRenderer.invoke("qps:open-screen-settings"),
  saveConfig: (patch: Record<string, unknown>) => ipcRenderer.invoke("qps:save-config", patch),
  setSpatialReady: (ready: boolean) => ipcRenderer.send("qps:spatial-ready", ready),
  onSignalingChanged: (cb: (state: { url: string; source: string }) => void) => {
    ipcRenderer.on("qps:signaling-changed", (_event, state) => cb(state));
  },
  onConfigState: (cb: (state: Record<string, unknown>) => void) => {
    ipcRenderer.on("qps:config-state", (_event, state) => cb(state));
  }
});
