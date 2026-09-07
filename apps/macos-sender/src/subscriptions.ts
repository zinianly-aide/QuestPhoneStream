export type SubscriptionPhase = "requested" | "created" | "active";

export interface SubscriptionState {
  capability: string;
  requestId: string;
  subscriptionId: string;
  phase: SubscriptionPhase;
}

/** Tracks the control-plane lifecycle independently from the telemetry DataChannel. */
export class SpatialSubscriptionTracker {
  private readonly byCapability = new Map<string, SubscriptionState>();

  begin(capability: string, requestId: string): boolean {
    if (!capability || !requestId || this.byCapability.has(capability)) return false;
    this.byCapability.set(capability, { capability, requestId, subscriptionId: "", phase: "requested" });
    return true;
  }

  markCreated(correlationId: string, subscriptionId: string, capability = ""): SubscriptionState | null {
    if (!correlationId) return null;
    const current = [...this.byCapability.values()].find(item => item.requestId === correlationId);
    if (!current || (capability && current.capability !== capability)) return null;
    const next: SubscriptionState = {
      ...current,
      subscriptionId,
      phase: "created"
    };
    this.byCapability.set(current.capability, next);
    return { ...next };
  }

  markActive(capability: string): boolean {
    const current = this.byCapability.get(capability);
    if (!current || current.phase === "requested") return false;
    if (current.phase === "active") return true;
    this.byCapability.set(capability, { ...current, phase: "active" });
    return true;
  }

  markCreatedSubscriptionsActive(): void {
    for (const [capability, current] of this.byCapability) {
      if (current.phase === "created") this.byCapability.set(capability, { ...current, phase: "active" });
    }
  }

  close(subscriptionId = "", capability = ""): string | null {
    let target = capability ? this.byCapability.get(capability) : undefined;
    if (!target && subscriptionId) {
      target = [...this.byCapability.values()].find(item => item.subscriptionId === subscriptionId);
    }
    if (!target) return null;
    this.byCapability.delete(target.capability);
    return target.capability;
  }

  fail(correlationId: string): string | null {
    if (!correlationId) return null;
    const target = [...this.byCapability.values()].find(item => item.requestId === correlationId);
    if (!target) return null;
    this.byCapability.delete(target.capability);
    return target.capability;
  }

  phase(capability: string): SubscriptionPhase | null {
    return this.byCapability.get(capability)?.phase ?? null;
  }

  has(capability: string): boolean {
    return this.byCapability.has(capability);
  }

  reset(): void {
    this.byCapability.clear();
  }

  snapshot(): SubscriptionState[] {
    return [...this.byCapability.values()].map(item => ({ ...item }));
  }
}
