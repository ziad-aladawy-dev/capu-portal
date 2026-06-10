import { describe, it, expect } from "vitest";
import { computeGpa, projectCgpa, GRADE_POINTS } from "../core/services/gradeService";

describe("computeGpa", () => {
  it("returns 0 for empty/ungraded input", () => {
    expect(computeGpa([])).toBe(0);
    expect(computeGpa([{ credits: 3, grade: undefined }])).toBe(0);
  });

  it("weights by credit hours", () => {
    // 3cr A(4.0) + 1cr B(3.0) = (12 + 3)/4 = 3.75
    expect(computeGpa([{ credits: 3, grade: "A" }, { credits: 1, grade: "B" }])).toBeCloseTo(3.75, 2);
  });

  it("ignores rows with unknown grades or zero credits", () => {
    expect(computeGpa([{ credits: 3, grade: "A" }, { credits: 0, grade: "F" }, { credits: 3, grade: "W" }])).toBeCloseTo(4.0, 2);
  });
});

describe("projectCgpa", () => {
  it("returns prior CGPA when no planned rows", () => {
    expect(projectCgpa({ priorCredits: 30, priorCgpa: 3.0, plannedRows: [] })).toBeCloseTo(3.0, 2);
  });

  it("blends prior CGPA with planned grades by credit weight", () => {
    // prior: 30cr @ 3.0 = 90 points; planned: 6cr A(4.0) = 24; total 114/36 = 3.1667
    const r = projectCgpa({ priorCredits: 30, priorCgpa: 3.0, plannedRows: [{ credits: 6, grade: "A" }] });
    expect(r).toBeCloseTo(3.1667, 3);
  });

  it("ignores planned rows without a valid grade", () => {
    const r = projectCgpa({ priorCredits: 10, priorCgpa: 2.0, plannedRows: [{ credits: 3, grade: "" }] });
    expect(r).toBeCloseTo(2.0, 2);
  });

  it("handles zero prior credits (pure planned)", () => {
    const r = projectCgpa({ priorCredits: 0, priorCgpa: 0, plannedRows: [{ credits: 3, grade: "B" }] });
    expect(r).toBeCloseTo(GRADE_POINTS.B, 2);
  });
});
