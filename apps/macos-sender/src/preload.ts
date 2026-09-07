import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("qps", {
  getConfig: () => ipcRenderer.invoke("qps:get-config"),
  listSources: () => ipcRenderer.invoke("qps:list-sources"),
  setSpatialReady: (ready: boolean) => ipcRenderer.send("qps:spatial-ready", ready)
});
