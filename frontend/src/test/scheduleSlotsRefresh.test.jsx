import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

vi.mock("../core/contexts/DomainContext", () => ({
  useDomain: () => ({ scopeNode: { id: "n1", name: "Node A" } }),
}));
vi.mock("../core/contexts/AcademicContext", () => ({
  useAcademic: () => ({ selectedSemesterObj: { id: "s1", name: "Fall 2026" }, selectedSemester: "Fall 2026" }),
}));
vi.mock("../core/components/Toast", () => ({ useToast: () => ({ addToast: vi.fn() }) }));
vi.mock("../core/auth/PermissionGate", () => ({ default: ({ children }) => children }));
vi.mock("../modules/schedule/components/DraggableScheduleGrid", () => ({ default: () => null }));
vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: (k) => k, i18n: { language: "en" } }) }));

vi.mock("../core/query/useCourses", () => ({
  useActiveCourses: () => ({
    data: [{ id: "c1", code: "CS101", title: "Intro to CS", creditHours: 3 }],
  }),
}));

const refetchSpy = vi.fn();
vi.mock("../core/query/useScheduleSlots", () => ({
  useScheduleSlots: () => ({ data: [], isLoading: false, refetch: refetchSpy }),
  useOfferingsForSchedule: () => ({
    data: [{ id: "o1", courseCode: "CS101", courseTitle: "Intro", sectionCode: "A", registeredCount: 5, capacity: 30 }],
  }),
  useCreateSlot: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUpdateSlot: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteSlot: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCloseSlot: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useOpenSlot: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useBatchCreateSlots: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

import ScheduleSlotsPage from "../modules/schedule/pages/ScheduleSlotsPage";

beforeEach(() => vi.clearAllMocks());

describe("ScheduleSlotsPage refresh wiring (runtime)", () => {
  it("calls the slots query refetch when Refresh is clicked", () => {
    render(<ScheduleSlotsPage />);
    // select an offering so the toolbar (incl. Refresh) appears
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "o1" } });
    const refreshBtn = screen.getByRole("button", { name: "refresh" });
    fireEvent.click(refreshBtn);
    expect(refetchSpy).toHaveBeenCalledTimes(1);
  });
});
