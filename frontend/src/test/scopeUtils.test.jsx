import { describe, it, expect } from "vitest";
import { findNode, isOutsideActiveScope } from "../modules/permissions/utils/scopeUtils";
import { studentDirectoryConfig, staffDirectoryConfig } from "../core/components/directory/directoryConfigs";

const TREE = [
  {
    id: "uni",
    name: "University",
    children: [
      {
        id: "eng",
        name: "Engineering",
        children: [
          { id: "cs", name: "Computer Science", children: [] },
        ],
      },
      { id: "med", name: "Medicine", children: [] },
    ],
  },
];

describe("scopeUtils.findNode", () => {
  it("finds nested nodes by id", () => {
    expect(findNode(TREE, "cs")?.name).toBe("Computer Science");
    expect(findNode(TREE, "uni")?.name).toBe("University");
    expect(findNode(TREE, "missing")).toBeNull();
  });
});

describe("scopeUtils.isOutsideActiveScope", () => {
  it("is never outside when no active scope or a global assignment", () => {
    expect(isOutsideActiveScope(TREE, null, "med")).toBe(false);
    expect(isOutsideActiveScope(TREE, "eng", null)).toBe(false);
  });

  it("accepts nodes within the active subtree", () => {
    expect(isOutsideActiveScope(TREE, "eng", "cs")).toBe(false);
    expect(isOutsideActiveScope(TREE, "eng", "eng")).toBe(false);
  });

  it("accepts ancestors of the active node (they cover it)", () => {
    expect(isOutsideActiveScope(TREE, "cs", "eng")).toBe(false);
    expect(isOutsideActiveScope(TREE, "eng", "uni")).toBe(false);
  });

  it("flags sibling branches as outside", () => {
    expect(isOutsideActiveScope(TREE, "eng", "med")).toBe(true);
    expect(isOutsideActiveScope(TREE, "med", "cs")).toBe(true);
  });
});

describe("directory detail routing (Scenario A)", () => {
  it("students resolve to the student detail page, not the generic user page", () => {
    expect(studentDirectoryConfig.routes.detail("abc")).toBe("/admin/students/abc");
  });

  it("staff resolve to the generic user detail page", () => {
    expect(staffDirectoryConfig.routes.detail("xyz")).toBe("/admin/users/xyz");
  });
});
