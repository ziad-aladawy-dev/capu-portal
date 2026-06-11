import { describe, it, expect, beforeEach, vi } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

vi.mock("../modules/users/services/userService", () => ({
  default: {
    getStudentById: vi.fn(),
    updateStudent: vi.fn(),
    toggleStudentStatus: vi.fn(),
    deleteStudent: vi.fn(),
  },
}));

import userService from "../modules/users/services/userService";
import { useUpdateStudent, studentKey } from "../core/query/useStudents";

let qc;
const wrapper = ({ children }) => (
  <QueryClientProvider client={qc}>{children}</QueryClientProvider>
);

beforeEach(() => {
  vi.clearAllMocks();
  qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: 0 } },
  });
});

describe("useUpdateStudent write-through (Scenario A state sync)", () => {
  it("sends the update and refetches the student detail cache", async () => {
    qc.setQueryData(studentKey("s1"), { id: "s1", name: "Old Name", studentCode: "STU-1" });
    userService.updateStudent.mockResolvedValue({ message: "updated" });
    const spy = vi.spyOn(qc, "invalidateQueries");

    const { result } = renderHook(() => useUpdateStudent(), { wrapper });
    await act(async () => {
      await result.current.mutateAsync({ id: "s1", nameEn: "New Name" });
    });

    expect(userService.updateStudent).toHaveBeenCalledWith("s1", { nameEn: "New Name" });
    // The endpoint returns only a confirmation, so the detail entry must be
    // refetched rather than primed from the response.
    expect(spy).toHaveBeenCalledWith({ queryKey: studentKey("s1") });
  });

  it("invalidates every directory list so tables and the sidebar refetch", async () => {
    userService.updateStudent.mockResolvedValue({ message: "updated" });
    const spy = vi.spyOn(qc, "invalidateQueries");

    const { result } = renderHook(() => useUpdateStudent(), { wrapper });
    await act(async () => {
      await result.current.mutateAsync({ id: "s1", nameEn: "X" });
    });

    await waitFor(() => {
      expect(spy).toHaveBeenCalledWith({ queryKey: ["directory"] });
    });
  });
});
