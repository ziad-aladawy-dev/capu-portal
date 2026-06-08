# Impact Analysis: IManifest vs. PermissionManifest

Implementing the `IManifest` interface to replace or augment the existing `PermissionManifest` system represents a significant architectural upgrade for the Capital University Portal.

---

## 1. Architectural Impact
Implementing `IManifest` provides a much richer **Module Identity** than the current security-focused `PermissionManifest`.

*   **Dynamic UI Orchestration**: The `MenuItems` property allows the backend to drive the frontend navigation. Instead of hardcoding the sidebar, the frontend can query a central "System Manifest" to build its menu dynamically based on the user's permissions and the installed modules.
*   **Startup Dependency Validation**: The `DependsOn` metadata enables a startup "Health Check". The system can build a dependency graph and throw an exception if a required module is missing or if there is a version mismatch.
*   **Database Schema Isolation**: The `Backend.Schema` property provides a formal path toward **Schema-per-Module**. This prevents modules from reaching into each other's tables and enforces a cleaner separation of data concerns.
*   **Contract Documentation**: The `Events` property acts as a living catalog for the Event-Driven architecture (Outbox pattern). It explicitly lists what events each module `Publishes` and `Subscribes` to.

---

## 2. Migration Cost Breakdown
The migration is "Wide but Shallow"—it touches many files but requires relatively simple changes in each.

| Phase | Tasks | Estimated Effort |
| :--- | :--- | :--- |
| **1. Consolidation** | Update `IManifest` to include the `Resources` collection from `IPermissionManifest`. | 2 Hours |
| **2. Module Refactor** | Update all existing manifests (10+) to implement `IManifest`. | 4 Hours |
| **3. Infrastructure** | Update `PermissionManifestSynchronizer` and build a `ManifestRegistry`. | 6 Hours |
| **4. API & UI** | Create `GET /api/system/manifest` and update frontend dynamic menu logic. | 6 Hours |
| **Total** | | **~18-20 Hours** |

---

## 3. Comparison Table

| Feature | `PermissionManifest` (Current) | `IManifest` (Proposed) |
| :--- | :--- | :--- |
| **Primary Goal** | Define Access Control (RBAC). | Define Module Identity & Integration. |
| **Scope** | Security only. | Security, UI, Data Schema, Events, Dependencies. |
| **Maintainability** | Logic is scattered. | Centralized "Single Source of Truth" per module. |

---

## 4. Recommendation
The migration is highly recommended for long-term scalability and to support future transitions to Micro-frontends or Microservices.
