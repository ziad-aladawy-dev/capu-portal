import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

vi.mock("../core/api/apiClient", () => ({
  default: {
    get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn(),
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  },
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (k, opts) => opts?.defaultValue ?? k,
    i18n: { language: "en" },
  }),
}));

import ProfileRecordFormModal from "../modules/studentProfileRecords/components/ProfileRecordFormModal";

let onSubmit, onClose;

beforeEach(() => {
  vi.clearAllMocks();
  onSubmit = vi.fn();
  onClose = vi.fn();
});

const renderCreate = () => render(
  <ProfileRecordFormModal mode="create" record={null} pending={false} onSubmit={onSubmit} onClose={onClose} />
);

describe("ProfileRecordFormModal (react-hook-form + zod)", () => {
  it("rejects an invalid JSON payload with an inline error and no submit", async () => {
    renderCreate();
    fireEvent.change(document.querySelector(".spr-form-textarea"), {
      target: { value: "{ not json" },
    });
    fireEvent.click(screen.getByRole("button", { name: "create" }));
    await waitFor(() => {
      expect(screen.getByText("Data payload must be valid JSON.")).toBeInTheDocument();
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("requires a custom key when the Custom category is selected", async () => {
    renderCreate();
    fireEvent.change(document.querySelector(".spr-form-select"), { target: { value: "0" } });
    fireEvent.click(screen.getByRole("button", { name: "create" }));
    await waitFor(() => {
      expect(screen.getByText("Custom category key is required for the Custom category.")).toBeInTheDocument();
    });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("submits a normalised payload (compact JSON, numeric fields)", async () => {
    renderCreate();
    fireEvent.change(document.querySelector(".spr-form-textarea"), {
      target: { value: '{\n  "bloodType": "O+"\n}' },
    });
    fireEvent.click(screen.getByRole("button", { name: "create" }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit).toHaveBeenCalledWith({
      category: 1,
      customCategoryKey: null,
      schemaVersion: 1,
      dataJson: '{"bloodType":"O+"}',
      isSensitive: false,
    });
  });

  it("pre-fills and prettifies an existing record in edit mode", () => {
    render(
      <ProfileRecordFormModal
        mode="edit"
        record={{ category: 2, customCategoryKey: "", schemaVersion: 3, dataJson: '{"a":1}', isSensitive: true }}
        pending={false}
        onSubmit={onSubmit}
        onClose={onClose}
      />
    );
    expect(document.querySelector(".spr-form-textarea").value).toContain('"a": 1');
    expect(document.querySelector(".spr-form-select")).toBeDisabled();
    expect(screen.getByRole("button", { name: "save" })).toBeInTheDocument();
  });
});
