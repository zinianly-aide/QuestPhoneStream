import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("qps", {
  getConfig: () => ipcRenderer.invoke("qps:get-config"),
  listSources: () => ipcRenderer.invoke("qps:list-sources"),
  saveConfig: (patch: Record<string, unknown>) => ipcRenderer.invoke("qps:save-config", patch),
  setSpatialReady: (ready: boolean) => ipcRenderer.send("qps:spatial-ready", ready),
  onSignalingChanged: (cb: (state: { url: string; source: string }) => void) => {
    ipcRenderer.on("qps:signaling-changed", (_event, state) => cb(state));
  },
  onConfigState: (cb: (state: Record<string, unknown>) => void) => {
    ipcRenderer.on("qps:config-state", (_event, state) => cb(state));
  }
});
