import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

// --- Mocks ---
vi.mock("../core/api/apiClient", () => ({
  default: {
    get: vi.fn(), post: vi.fn(), patch: vi.fn(), delete: vi.fn(),
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
    defaults: { baseURL: "" },
  },
}));
vi.mock("../core/query/useCourses", () => ({
  useCourses: vi.fn(),
  useCreateCourse: vi.fn(),
  useUpdateCourse: vi.fn(),
  useDeleteCourse: vi.fn(),
  useCloseCourse: vi.fn(),
  useOpenCourse: vi.fn(),
  useBulkDeleteCourses: vi.fn(),
}));
vi.mock("../core/auth/PermissionGate", () => ({ default: ({ children }) => children }));
vi.mock("../core/components/Toast", () => ({ useToast: () => ({ addToast: vi.fn() }) }));

import * as hooks from "../core/query/useCourses";
import CoursesPage from "../modules/courses/pages/CoursesPage";

const COURSES = [
  { id: "c1", code: "CS101", title: "Intro to CS", creditHours: 3, category: 1, isActive: true, isClosed: false },
  { id: "c2", code: "MA200", title: "Calculus II", creditHours: 4, category: 2, isActive: false, isClosed: true },
];

let createMut, updateMut, deleteMut, closeMut, openMut, bulkMut;

const renderPage = () => render(<MemoryRouter><CoursesPage /></MemoryRouter>);

beforeEach(() => {
  vi.clearAllMocks();
  createMut = { mutateAsync: vi.fn().mockResolvedValue({}), isPending: false };
  updateMut = { mutateAsync: vi.fn().mockResolvedValue({}), isPending: false };
  deleteMut = { mutateAsync: vi.fn().mockResolvedValue({}), isPending: false };
  closeMut = { mutateAsync: vi.fn().mockResolvedValue({}), isPending: false };
  openMut = { mutateAsync: vi.fn().mockResolvedValue({}), isPending: false };
  bulkMut = { mutateAsync: vi.fn().mockResolvedValue({}), isPending: false };

  hooks.useCourses.mockReturnValue({
    data: { items: COURSES, totalPages: 1, totalCount: 2 },
    isLoading: false, error: null, refetch: vi.fn(),
  });
  hooks.useCreateCourse.mockReturnValue(createMut);
  hooks.useUpdateCourse.mockReturnValue(updateMut);
  hooks.useDeleteCourse.mockReturnValue(deleteMut);
  hooks.useCloseCourse.mockReturnValue(closeMut);
  hooks.useOpenCourse.mockReturnValue(openMut);
  hooks.useBulkDeleteCourses.mockReturnValue(bulkMut);
});

describe("CoursesPage (runtime)", () => {
  it("renders the catalog, stat strip and status filter", () => {
    renderPage();
    expect(screen.getByText("CS101")).toBeInTheDocument();
    expect(screen.getByText("Calculus II")).toBeInTheDocument();
    expect(screen.getByText("Total Courses")).toBeInTheDocument();
    // new working status filter option
    expect(screen.getByRole("option", { name: "Active only" })).toBeInTheDocument();
  });

  it("no longer exposes the removed dead features (Health / Prerequisites)", () => {
    renderPage();
    expect(screen.queryByText(/Health/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Prerequisites/i)).not.toBeInTheDocument();
    // the old dead "Offerings" column header is gone (no column with that exact header)
    expect(screen.queryByRole("columnheader", { name: "Offerings" })).not.toBeInTheDocument();
  });

  it("creates a course, calling the mutation with the form payload", async () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: /Create Course/i }));
    const dialog = screen.getByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText("Code"), { target: { value: "PHY101" } });
    fireEvent.change(within(dialog).getByLabelText("Title"), { target: { value: "Physics I" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(createMut.mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({ code: "PHY101", title: "Physics I" })
      )
    );
  });

  it("edits a course, sending only mutable fields (not the code)", async () => {
    renderPage();
    fireEvent.click(screen.getAllByTitle("Edit")[0]);
    const dialog = screen.getByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText("Title"), { target: { value: "Intro to CS — Revised" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save Changes" }));

    await waitFor(() =>
      expect(updateMut.mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({ id: "c1", title: "Intro to CS — Revised" })
      )
    );
    // code must NOT be part of the update payload
    expect(updateMut.mutateAsync.mock.calls[0][0]).not.toHaveProperty("code");
  });

  it("deletes a course after confirmation", async () => {
    renderPage();
    fireEvent.click(screen.getAllByTitle("Delete")[0]);
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByText("Delete Course")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(deleteMut.mutateAsync).toHaveBeenCalledWith("c1"));
  });
});
