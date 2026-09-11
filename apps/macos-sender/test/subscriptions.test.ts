import { describe, expect, it } from "vitest";
import { SpatialSubscriptionTracker } from "../src/subscriptions";

describe("SpatialSubscriptionTracker", () => {
  it("tracks requested -> created -> active by correlationId", () => {
    const tracker = new SpatialSubscriptionTracker();
    expect(tracker.begin("xr.head.pose", "req-1")).toBe(true);
    expect(tracker.phase("xr.head.pose")).toBe("requested");
    expect(tracker.markCreated("req-1", "sub-1", "xr.head.pose")?.phase).toBe("created");
    expect(tracker.markActive("xr.head.pose")).toBe(true);
    expect(tracker.phase("xr.head.pose")).toBe("active");
  });

  it("clears a failed request so retry is possible", () => {
    const tracker = new SpatialSubscriptionTracker();
    tracker.begin("xr.controller.pose", "req-1");
    expect(tracker.fail("req-1")).toBe("xr.controller.pose");
    expect(tracker.phase("xr.controller.pose")).toBeNull();
    expect(tracker.begin("xr.controller.pose", "req-2")).toBe(true);
  });

  it("clears closed and reset subscriptions for reconnect/resubscribe", () => {
    const tracker = new SpatialSubscriptionTracker();
    tracker.begin("xr.hand.pose", "req-1");
    tracker.markCreated("req-1", "sub-1", "xr.hand.pose");
    expect(tracker.close("sub-1")).toBe("xr.hand.pose");
    expect(tracker.begin("xr.hand.pose", "req-2")).toBe(true);
    tracker.reset();
    expect(tracker.snapshot()).toEqual([]);
    expect(tracker.begin("xr.hand.pose", "req-3")).toBe(true);
  });
});
