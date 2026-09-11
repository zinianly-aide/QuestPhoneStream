import { readFileSync } from "node:fs";
import { expect, test } from "vitest";

function schema(name: string): any {
  return JSON.parse(readFileSync(new URL(`../../../protocol/spatial/${name}.schema.json`, import.meta.url), "utf8"));
}

test("envelope requires the complete v1 routing metadata", () => {
  expect(schema("envelope").required).toEqual([
    "v", "id", "type", "source", "target", "sessionId", "streamId", "correlationId", "timestamp", "payload",
  ]);
});

test("capability descriptor carries state, transports, features, limits and permissions", () => {
  const capability = schema("capability");
  expect(capability.required).toEqual(expect.arrayContaining(["name", "version", "state", "transports", "features", "limits", "permissions"]));
  expect(capability.properties.state.required).toEqual(["available", "authorized", "active"]);
});

test("SpatialPose always identifies its reference space", () => {
  const spatial = schema("spatial");
  expect(spatial.$defs.SpatialPose.required).toEqual(expect.arrayContaining(["space", "timestamp", "position", "orientation"]));
});

test("subscription negotiation includes rate, format, transport and reliability", () => {
  expect(schema("subscription").required).toEqual(expect.arrayContaining(["rateHz", "format", "transport", "reliability"]));
  const types = schema("envelope").properties.type.enum;
  for (const type of ["subscription.create", "subscription.created", "subscription.cancel", "subscription.closed"])
    expect(types).toContain(type);
});
