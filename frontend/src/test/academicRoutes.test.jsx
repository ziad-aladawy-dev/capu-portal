import { describe, it, expect, vi } from "vitest";

// Route metadata sanity for the unified Academic Suite. Catches dead links
// from the sidebar/command palette without rendering any page.
vi.mock("react", async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, lazy: () => () => null };
});

import academicRoutes from "../modules/academic/routes";

describe("academic module routes", () => {
  const pages = academicRoutes.filter((r) => !r.isRedirect);
  const redirects = academicRoutes.filter((r) => r.isRedirect);

  it("declares all six suite pages under /admin/academic/", () => {
    const paths = pages.map((r) => r.path);
    expect(paths).toEqual([
      "/admin/academic/courses",
      "/admin/academic/offerings",
      "/admin/academic/plans",
      "/admin/academic/programs",
      "/admin/academic/schedule",
      "/admin/academic/scheduling-matrix",
    ]);
  });

  it("every page carries a permission and a sidebar menu item", () => {
    for (const r of pages) {
      expect(r.permission, r.path).toBeTruthy();
      expect(r.menuItem?.category, r.path).toBe("Academic");
      expect(r.menuItem?.label, r.path).toBeTruthy();
    }
  });

  it("legacy paths redirect into the suite", () => {
    const map = Object.fromEntries(redirects.map((r) => [r.path, r.redirectTo]));
    expect(map["/admin/courses"]).toBe("/admin/academic/courses");
    expect(map["/admin/academic/course-hub"]).toBe("/admin/academic/courses");
    expect(map["/admin/academic-plans"]).toBe("/admin/academic/plans");
    expect(map["/admin/academic/course-offerings"]).toBe("/admin/academic/offerings");
    expect(map["/admin/programs"]).toBe("/admin/academic/programs");
  });

  it("redirect targets all resolve to declared pages", () => {
    const pagePaths = new Set(pages.map((r) => r.path));
    for (const r of redirects) {
      expect(pagePaths.has(r.redirectTo), `${r.path} → ${r.redirectTo}`).toBe(true);
    }
  });
});
