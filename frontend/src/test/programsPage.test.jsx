import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";

// --- Mocks (hoisted) ---
vi.mock("../core/services/structureService", () => ({
  fetchPrograms: vi.fn(),
  fetchFaculties: vi.fn(),
  fetchStructureNode: vi.fn(),
  createStructureNode: vi.fn(),
  updateStructureNode: vi.fn(),
  deleteStructureNode: vi.fn(),
}));
vi.mock("../core/auth/PermissionGate", () => ({ default: ({ children }) => children }));
vi.mock("../core/components/Toast", () => ({ useToast: () => ({ addToast: vi.fn() }) }));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (k) => k, i18n: { language: "en" } }),
}));

import * as structureService from "../core/services/structureService";
import ProgramsPage from "../modules/programs/pages/ProgramsPage";

const PROGRAMS = [
  { id: "p1", name: { en: "Computer Science", ar: "علوم الحاسب" }, parentId: "f1" },
  { id: "p2", name: { en: "Mechanical Engineering", ar: "" }, parentId: null },
];
const FACULTIES = [{ id: "f1", name: { en: "Engineering" } }];

beforeEach(() => {
  vi.clearAllMocks();
  structureService.fetchPrograms.mockResolvedValue(PROGRAMS);
  structureService.fetchFaculties.mockResolvedValue(FACULTIES);
  structureService.fetchStructureNode.mockImplementation((id) =>
    Promise.resolve(FACULTIES.find((f) => f.id === id) || null)
  );
  structureService.createStructureNode.mockResolvedValue({ id: "p3" });
  structureService.updateStructureNode.mockResolvedValue({});
  structureService.deleteStructureNode.mockResolvedValue({});
});

describe("ProgramsPage (runtime)", () => {
  it("loads and renders programs with faculty + unassigned badges", async () => {
    render(<ProgramsPage />);
    expect(await screen.findByText("Computer Science")).toBeInTheDocument();
    expect(screen.getByText("Mechanical Engineering")).toBeInTheDocument();
    // faculty resolved from lookup (appears as a row badge and as a filter option)
    expect(screen.getAllByText("Engineering").length).toBeGreaterThan(0);
    // p2 has no faculty -> "Unassigned" (shown as a row badge and as a stat label)
    expect(screen.getAllByText("Unassigned").length).toBeGreaterThan(0);
    // stat cards present
    expect(screen.getByText("Total Programs")).toBeInTheDocument();
    expect(screen.getByText("Faculties")).toBeInTheDocument();
  });

  it("filters by search text", async () => {
    render(<ProgramsPage />);
    await screen.findByText("Computer Science");
    fireEvent.change(screen.getByPlaceholderText(/Search programs/i), { target: { value: "mechanical" } });
    expect(screen.queryByText("Computer Science")).not.toBeInTheDocument();
    expect(screen.getByText("Mechanical Engineering")).toBeInTheDocument();
  });

  it("creates a program through the drawer, calling the service with the right payload", async () => {
    render(<ProgramsPage />);
    await screen.findByText("Computer Science");

    fireEvent.click(screen.getByRole("button", { name: /Add Program/i }));
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("New Program")).toBeInTheDocument();

    fireEvent.change(within(dialog).getByPlaceholderText("e.g. Computer Science"), { target: { value: "Data Science" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(structureService.createStructureNode).toHaveBeenCalledWith(
        expect.objectContaining({
          type: "Program",
          name: expect.objectContaining({ en: "Data Science" }),
        })
      )
    );
  });

  it("blocks create when the English name is empty", async () => {
    render(<ProgramsPage />);
    await screen.findByText("Computer Science");
    fireEvent.click(screen.getByRole("button", { name: /Add Program/i }));
    const dialog = screen.getByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: "Create" }));
    expect(await within(dialog).findByText(/English name is required/i)).toBeInTheDocument();
    expect(structureService.createStructureNode).not.toHaveBeenCalled();
  });

  it("deletes a program after confirmation, calling the service with the id", async () => {
    render(<ProgramsPage />);
    await screen.findByText("Computer Science");

    fireEvent.click(screen.getAllByTitle("Delete")[0]);
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("Delete Program")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(structureService.deleteStructureNode).toHaveBeenCalledWith("p1"));
  });
});
