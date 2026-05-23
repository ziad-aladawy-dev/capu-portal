import { describe, it, expect } from "vitest";
import { login, forgotPassword } from "../core/auth/authService";

// Mock apiClient
vi.mock("../core/api/apiClient", () => ({
  default: {
    post: vi.fn(),
    get: vi.fn(),
    setToken: vi.fn(),
    setRefreshToken: vi.fn(),
    clearTokens: vi.fn(),
    interceptors: {
      request: { use: vi.fn() },
      response: { use: vi.fn() },
    },
    defaults: { baseURL: "http://localhost:5256/api" },
  },
}));

describe("authService", () => {
  it("login returns expected shape", async () => {
    const api = (await import("../core/api/apiClient")).default;
    api.post.mockResolvedValue({
      data: {
        token: "test-token",
        refreshToken: "test-refresh",
        user: { id: 1, name: "Test" },
        permissions: [],
        authorizedScopes: { allowedNodeIds: [], allowedAcademicYearIds: [], allowedSemesterIds: [] },
        activeScope: { structural: { nodeId: null }, temporal: { academicYearId: null, semesterId: null } },
      },
    });

    const result = await login("admin", "pass");
    expect(result).toHaveProperty("token", "test-token");
    expect(result).toHaveProperty("refreshToken", "test-refresh");
    expect(result.user).toHaveProperty("name", "Test");
  });

  it("forgotPassword calls the correct endpoint", async () => {
    const api = (await import("../core/api/apiClient")).default;
    api.post.mockResolvedValue({ data: { success: true } });

    const result = await forgotPassword({ email: "test@test.com" });
    expect(api.post).toHaveBeenCalledWith("/auth/forgot-password", { email: "test@test.com" });
    expect(result).toEqual({ success: true });
  });
});

describe("permissionService", () => {
  it("fetchAllRoles returns data", async () => {
    const api = (await import("../core/api/apiClient")).default;
    const { fetchAllRoles } = await import("../core/services/permissionService");
    api.get.mockResolvedValue({ data: { items: [{ id: 1, name: "Admin" }], totalCount: 1 } });

    const result = await fetchAllRoles({ page: 1, pageSize: 10 });
    expect(api.get).toHaveBeenCalledWith("/roles", { params: { page: 1, pageSize: 10 } });
    expect(result.items).toHaveLength(1);
  });
});

describe("Toast", () => {
  it("ToastProvider renders children", async () => {
    const { render, screen } = await import("@testing-library/react");
    const { ToastProvider } = await import("../core/components/Toast");

    render(
      <ToastProvider>
        <div data-testid="child">Hello</div>
      </ToastProvider>
    );

    expect(screen.getByTestId("child")).toHaveTextContent("Hello");
  });
});
