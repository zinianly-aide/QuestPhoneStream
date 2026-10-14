import { describe, expect, it, vi } from "vitest";
import {
  parseTlsConnectServices, parseWirelessDevices, WirelessAdbAutoConnect
} from "../src/adbAutoConnect";

const serviceOutput = [
  "List of discovered mdns services",
  "adb-quest._adb-tls-connect._tcp.  192.168.50.24:38567",
  "adb-pair._adb-tls-pairing._tcp.  192.168.50.24:45021",
  "adb-bad._adb-tls-connect._tcp.  999.168.50.24:5555",
  "adb-quest._adb-tls-connect._tcp.  192.168.50.24:38567"
].join("\n");

describe("Quest wireless ADB mDNS", () => {
  it("uses only valid dynamic TLS connect endpoints", () => {
    expect(parseTlsConnectServices(serviceOutput)).toEqual(["192.168.50.24:38567"]);
    expect(parseTlsConnectServices("")).toEqual([]);
  });

  it("never calls a device connected when it is unauthorized or USB only", () => {
    const output = "List of devices attached\n" +
      "192.168.50.24:38567 unauthorized\n" +
      "R4ABC123 device usb:1-1 product:quest\n" +
      "192.168.50.25:42357 device product:quest\n";
    expect(parseWirelessDevices(output)).toEqual(["192.168.50.25:42357"]);
  });

  it("starts the official ADB daemon and observes paired devices without adb connect", async () => {
    const runner = vi.fn(async (args: string[]) => {
      if (args[0] === "mdns") return serviceOutput;
      if (args[0] === "devices") return "List of devices attached\n";
      return "";
    });
    const update = vi.fn();
    const service = new WirelessAdbAutoConnect(update, runner);
    const result = await service.refresh();
    expect(result.available).toBe(true);
    expect(result.services).toEqual(["192.168.50.24:38567"]);
    expect(result.connectedWireless).toEqual([]);
    expect(result.message).toContain("pair");
    expect(runner.mock.calls.map(c => c[0].join(" "))).toEqual([
      "start-server", "mdns services", "devices -l"
    ]);
    expect(update).toHaveBeenCalledOnce();
    service.stop();
  });

  it("deduplicates concurrent polls and surfaces missing adb", async () => {
    let release = () => {};
    const gate = new Promise<void>(resolve => { release = resolve; });
    const runner = vi.fn(async (args: string[]) => {
      if (args[0] === "start-server") {
        await gate;
        throw new Error("adb binary missing");
      }
      return "";
    });
    const service = new WirelessAdbAutoConnect(() => {}, runner);
    const first = service.refresh();
    const second = service.refresh();
    expect(first).toBe(second);
    release();
    const result = await first;
    expect(result.available).toBe(false);
    expect(result.message).toContain("adb binary missing");
    expect(runner).toHaveBeenCalledTimes(1);
    service.stop();
  });
});
