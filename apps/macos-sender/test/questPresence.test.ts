import { describe, expect, it } from "vitest";
import { parseQuestPresence } from "../src/discovery";

const valid = {
  name: "QuestPhoneStream Quest local",
  port: 41234,
  addresses: ["192.168.1.12", "fe80::abcd"],
  txt: { v: "1", id: "d".repeat(32), platform: "quest", caps: "scene" }
};

describe("Quest local mDNS broadcast", () => {
  it("parses app presence without assuming a working ADB port", () => {
    expect(parseQuestPresence(valid)).toEqual({
      id: "d".repeat(32), name: valid.name,
      addresses: ["192.168.1.12", "fe80::abcd"], port: 41234
    });
  });
  it("ignores other platforms, invalid IDs and invalid network ports", () => {
    expect(parseQuestPresence({ ...valid, txt: {...valid.txt, platform: "android"} })).toBeNull();
    expect(parseQuestPresence({ ...valid, txt: {...valid.txt, id: "../bad"} })).toBeNull();
    expect(parseQuestPresence({ ...valid, port: 0 })).toBeNull();
  });
  it("rejects addresses with unexpected symbols (presence is untrusted)", () => {
    const result = parseQuestPresence({ ...valid, addresses: ["192.168.1.12", "bad<script>"] });
    expect(result?.addresses).toEqual(["192.168.1.12"]);
  });
});
