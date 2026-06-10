import { describe, it, expect } from "vitest";
import { buildCompleteness } from "../core/hooks/useBlockerState";

const EMERGENCY = 3; // StudentProfileCategory.EmergencyContact

function emergencyRecord(obj) {
  return { category: EMERGENCY, dataJson: JSON.stringify(obj) };
}

describe("buildCompleteness", () => {
  it("reports 0% when nothing is filled", () => {
    const r = buildCompleteness({}, []);
    expect(r.overallPercentage).toBe(0);
    expect(r.missingFields.map((f) => f.key)).toEqual([
      "phoneNumber",
      "address",
      "emergencyName",
      "emergencyPhone",
    ]);
  });

  it("reports 100% when core + emergency are complete", () => {
    const student = { phoneNumber: "0100", address: "1 St" };
    const records = [emergencyRecord({ name: "Mom", phone: "0111" })];
    const r = buildCompleteness(student, records);
    expect(r.overallPercentage).toBe(100);
    expect(r.missingFields).toHaveLength(0);
  });

  it("treats blank/whitespace values as missing", () => {
    const student = { phoneNumber: "  ", address: "1 St" };
    const records = [emergencyRecord({ name: "Mom", phone: "" })];
    const r = buildCompleteness(student, records);
    // address + emergencyName filled; phone(core) + emergencyPhone missing => 50%
    expect(r.overallPercentage).toBe(50);
    expect(r.missingFields.map((f) => f.key).sort()).toEqual([
      "emergencyPhone",
      "phoneNumber",
    ]);
  });

  it("survives malformed emergency dataJson", () => {
    const student = { phoneNumber: "0100", address: "1 St" };
    const records = [{ category: EMERGENCY, dataJson: "{not json" }];
    const r = buildCompleteness(student, records);
    expect(r.overallPercentage).toBe(50); // core complete, emergency missing
  });

  it("ignores non-emergency profile records", () => {
    const student = { phoneNumber: "0100", address: "1 St" };
    const records = [{ category: 5, dataJson: JSON.stringify({ name: "x", phone: "y" }) }];
    const r = buildCompleteness(student, records);
    expect(r.overallPercentage).toBe(50);
  });
});
