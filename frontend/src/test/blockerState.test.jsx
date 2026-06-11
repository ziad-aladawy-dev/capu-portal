import { describe, it, expect } from "vitest";
import { buildCompleteness, CONTACT_RECORD_KEY } from "../core/hooks/useBlockerState";

const EMERGENCY = 3; // StudentProfileCategory.EmergencyContact
const CUSTOM = 0;    // StudentProfileCategory.Custom

function emergencyRecord(obj) {
  return { category: EMERGENCY, dataJson: JSON.stringify(obj) };
}

// Address/city/country live in a Custom record keyed CONTACT_RECORD_KEY —
// the Student entity has no address columns.
function contactRecord(obj) {
  return { category: CUSTOM, customKey: CONTACT_RECORD_KEY, dataJson: JSON.stringify(obj) };
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

  it("reports 100% when phone + contact record + emergency are complete", () => {
    const student = { phoneNumber: "0100" };
    const records = [
      contactRecord({ address: "1 St" }),
      emergencyRecord({ name: "Mom", phone: "0111" }),
    ];
    const r = buildCompleteness(student, records);
    expect(r.overallPercentage).toBe(100);
    expect(r.missingFields).toHaveLength(0);
  });

  it("treats blank/whitespace values as missing", () => {
    const student = { phoneNumber: "  " };
    const records = [
      contactRecord({ address: "1 St" }),
      emergencyRecord({ name: "Mom", phone: "" }),
    ];
    const r = buildCompleteness(student, records);
    // address + emergencyName filled; phone(core) + emergencyPhone missing => 50%
    expect(r.overallPercentage).toBe(50);
    expect(r.missingFields.map((f) => f.key).sort()).toEqual([
      "emergencyPhone",
      "phoneNumber",
    ]);
  });

  it("survives malformed emergency dataJson", () => {
    const student = { phoneNumber: "0100" };
    const records = [
      contactRecord({ address: "1 St" }),
      { category: EMERGENCY, dataJson: "{not json" },
    ];
    const r = buildCompleteness(student, records);
    expect(r.overallPercentage).toBe(50); // phone + address complete, emergency missing
  });

  it("ignores unrelated profile records (wrong category / wrong customKey)", () => {
    const student = { phoneNumber: "0100" };
    const records = [
      { category: 5, dataJson: JSON.stringify({ name: "x", phone: "y" }) },
      { category: CUSTOM, customKey: "something-else", dataJson: JSON.stringify({ address: "1 St" }) },
    ];
    const r = buildCompleteness(student, records);
    expect(r.overallPercentage).toBe(25); // only phoneNumber counts
  });

  it("exposes contactData for wizard prefill", () => {
    const records = [contactRecord({ address: "1 St", city: "Cairo" })];
    const r = buildCompleteness({}, records);
    expect(r.contactData).toEqual({ address: "1 St", city: "Cairo" });
  });
});
