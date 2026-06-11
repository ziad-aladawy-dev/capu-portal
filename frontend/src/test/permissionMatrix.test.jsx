import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (k, opts) => opts?.defaultValue ?? k,
    i18n: { language: "en" },
  }),
}));

import PermissionMatrix from "../modules/permissions/components/PermissionMatrix";
import { computeResourceLevel, ACTION_NAME_TO_LEVEL, LEVEL_TO_ACTION } from "../core/constants/permissionLevels";

const MODULES = [
  {
    moduleId: "m1",
    moduleName: "Students",
    resources: [
      {
        resourceId: "r1",
        resourceName: "Student Records",
        permissions: [
          { action: "View", isAssigned: true },
          { action: "Insert", isAssigned: false },
          { action: "EditClose", isAssigned: false },
        ],
      },
      {
        resourceId: "r2",
        resourceName: "Enrollment",
        permissions: [{ action: "View", isAssigned: false }],
      },
    ],
  },
];

describe("permissionLevels constants", () => {
  it("LEVEL_TO_ACTION is the inverse of ACTION_NAME_TO_LEVEL", () => {
    for (const [action, level] of Object.entries(ACTION_NAME_TO_LEVEL)) {
      expect(LEVEL_TO_ACTION[level]).toBe(action);
    }
  });

  it("computeResourceLevel returns the highest assigned level", () => {
    expect(computeResourceLevel([
      { action: "View", isAssigned: true },
      { action: "EditClose", isAssigned: true },
      { action: "Delete", isAssigned: false },
    ])).toBe(3);
    expect(computeResourceLevel([])).toBe(0);
    expect(computeResourceLevel(null)).toBe(0);
  });
});

describe("PermissionMatrix", () => {
  it("renders module group headers and resource rows", () => {
    render(
      <PermissionMatrix
        modules={MODULES}
        getLevel={() => 0}
        onLevelChange={() => {}}
      />
    );
    expect(screen.getByText("Students")).toBeInTheDocument();
    expect(screen.getByText("Student Records")).toBeInTheDocument();
    expect(screen.getByText("Enrollment")).toBeInTheDocument();
  });

  it("clicking an available level reports the toggled value", () => {
    const onChange = vi.fn();
    render(
      <PermissionMatrix
        modules={MODULES}
        getLevel={() => 1}
        onLevelChange={onChange}
      />
    );
    // "Student Records" supports Insert (level 2): clicking it sets level 2
    const insertBtn = screen.getByRole("button", { name: "Student Records: insert" });
    fireEvent.click(insertBtn);
    expect(onChange).toHaveBeenCalledWith("m1", MODULES[0].resources[0], 2);
    // Clicking the current level (View=1) toggles it back to 0
    const viewBtn = screen.getByRole("button", { name: "Student Records: view" });
    fireEvent.click(viewBtn);
    expect(onChange).toHaveBeenCalledWith("m1", MODULES[0].resources[0], 0);
  });

  it("levels without a backend action render disabled", () => {
    const onChange = vi.fn();
    render(
      <PermissionMatrix
        modules={MODULES}
        getLevel={() => 0}
        onLevelChange={onChange}
      />
    );
    // "Student Records" has no Delete action -> its Delete button is disabled
    const deleteBtn = screen.getByRole("button", { name: "Student Records: delete" });
    expect(deleteBtn).toBeDisabled();
    fireEvent.click(deleteBtn);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("defensive UI: read-only mode disables everything but keeps it visible", () => {
    const onChange = vi.fn();
    render(
      <PermissionMatrix
        modules={MODULES}
        getLevel={() => 1}
        onLevelChange={onChange}
        canEdit={false}
        disabledReason="No edit access"
      />
    );
    expect(screen.getByText("Read only")).toBeInTheDocument();
    const viewBtn = screen.getByRole("button", { name: "Student Records: view" });
    expect(viewBtn).toBeDisabled();
    expect(viewBtn).toHaveAttribute("title", "No edit access");
    fireEvent.click(viewBtn);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("search filters resources and empty modules disappear", () => {
    render(
      <PermissionMatrix
        modules={MODULES}
        getLevel={() => 0}
        onLevelChange={() => {}}
      />
    );
    fireEvent.change(screen.getByPlaceholderText("Search resources…"), { target: { value: "enroll" } });
    expect(screen.queryByText("Student Records")).not.toBeInTheDocument();
    expect(screen.getByText("Enrollment")).toBeInTheDocument();
  });
});
