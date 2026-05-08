# Authorization & Permissions

## 1. Feature Overview
The Authorization and Permissions feature manages access control for staff members within the CapitalUniversity application. It implements a Scope-First Hybrid RBAC (Role-Based Access Control) + ABAC (Attribute-Based Access Control) system. This means data visibility (Structural/Temporal Scopes) is evaluated to filter accessible records *before* evaluating permissions (Action Level) on those records.

Major responsibilities include:
- Storing and retrieving user role assignments and direct permission overrides.
- Validating and computing combinations of structural scopes (Faculty, Program) and temporal scopes (Academic Year, Semester).
- Evaluating whether a specific user can perform an action (Create, Read, Update, Delete, etc.) on a specific resource (e.g., "Student", "Course") within a given scope.
- Enforcing attribute-based constraints, specifically restricting certain actions (like Delete) on "closed" records unless overridden by higher-level privileges.

Integration points:
- Security Pipeline / Request Context Middleware
- Database Context (Entity Framework Core) for managing persistence
- Other feature modules (e.g., Student, Enrollment, Complaints) which check permissions before allowing operations.

## 2. Architecture Placement
The feature currently lives across several layers of the application:
- **API Layer:** `PermissionsController.cs` exposes HTTP endpoints.
- **Core Abstractions (`src/Core/Abstractions/Auth/Authorization`):** Defines interfaces (`IAuthorizationEvaluator`, `IPermissionManagementService`), DTOs, and base abstractions (`AuthorizationResult`, `ActionLevel`, `OverrideType`).
- **Core Application (`src/Core/Application/Auth/Authorization`):** Contains the business logic implementations: `PermissionManagementService` (handles management of assignments and overrides), `AuthorizationEvaluator` (handles in-memory policy checks), and `ScopeResolver`.
- **Core Domain (`src/Core/Domain/Identity`):** Defines the underlying EF Core persistence entities such as `StaffRoleAssignment`, `StaffPermissionOverride`, `StaffPermissionScope`, `RolePermission`, `RolePermissionScope`, and `Role`.
- **Core Infrastructure (`src/Core/Infrastructure/Persistence/Configurations`):** Contains EF Core entity type configurations for these tables.

Dependencies:
- Highly coupled to the `CoreDbContext` via EF Core in `PermissionManagementService`.
- Currently placed centrally inside the core architectural layer (`Core`), meaning other modules rely on it instead of it being an isolated plugin module itself.
- Intentional coupling exists between Identity domains (Users, Roles) and University Structure domains (Faculty, AcademicProgram, AcademicYear, Semester) to enforce scoping.

## 3. Data Flow

**Evaluating Access (Runtime Evaluation):**
```text
Controller / MediatR Handler
→ Request Context (builds Requested Scope)
→ ScopeResolver (computes Effective Scope)
→ AuthorizationEvaluator.Evaluate()
   → Matches against provided Overrides & Roles (RBAC Check)
   → Evaluates ABAC constraint (Closed status checks)
→ Returns AuthorizationResult (Allow/Deny)
```

**Managing Assignments (CRUD Operations):**
```text
PermissionsController
→ PermissionManagementService (reads/writes EF DbContext)
   → Validates Scope Combinations (Faculty vs AllFaculties, etc.)
   → Maps DTOs to Entities (StaffRoleAssignment, StaffPermissionOverride, StaffPermissionScope)
   → Saves to CoreDbContext
→ Maps Entities back to PermissionAssignmentResponse
→ Returns HTTP Response
```

## 4. Entity and Persistence Mapping
Related entities:
- `StaffRoleAssignment`: Maps a user (`StaffId`) to a `RoleId` within a specific `Domain`, `Year`, and `Semester`. Contains optional `FacultyId` and `ProgramId` columns.
- `StaffPermissionOverride`: Grants or denies direct permissions. Maps a user to a `ServiceId`, `Resource` string, `ActionLevel`, and `OverrideType`. Like roles, it carries `Domain`, `Year`, and `Semester`.
- `StaffPermissionScope`: Represents additional structural scopes (Faculty/Program) for a permission override.
- `RolePermission` and `RolePermissionScope`: Defines the permissions associated with a role.

Persistence Semantics:
- The system heavily relies on string properties for `Domain`, `Year`, and `Semester`. A value of `"Global"` denotes application-wide scope or "Always Active".
- The implementation does not use an explicit dedicated aggregate root with a single, stable identity for a "Permission Assignment" grouping. Instead, assignments are treated as logical API projections defined by a combination of User + Structural Scope (Domain) + Temporal Scope (Year, Semester).
- Managing assignments involves scoped queries mapping DTO updates to separate `StaffRoleAssignment` and `StaffPermissionOverride` EF instances dynamically without a parent aggregate ID.
- Removing overrides is performed by matching entity properties (ServiceId, Resource, Type) rather than matching by primary key.

## 5. DTOs and Contracts
Defined in `CapitalUniversity.Core.Abstractions.Auth.Authorization.DTOs.PermissionAssignmentContracts.cs`.

**Contracts:**
- `StructuralScopeModel`: Contains `FacultyId`, `AllFaculties`, `ProgramId`, `AllPrograms`.
- `TemporalScopeModel`: Contains `AcademicYearId`, `SemesterId`, `AlwaysActive`.
- `PermissionOverrideModel`: Contains `ServiceId`, `Resource`, `Level`, `Type`.

**Request/Response DTOs:**
- `GetPermissionAssignmentQueryDto`: Used in HTTP GET to fetch a logical assignment group.
- `CreatePermissionAssignmentRequest`: Payload to create roles and overrides for a scope.
- `UpdatePermissionAssignmentRequest`: Payload containing `RolesToAdd`, `RolesToRemove`, `PermissionsToAdd`, and `PermissionsToRemove` arrays for modifying an assignment grouping.
- `PermissionAssignmentResponse`: The aggregated structure returned to the client containing user ID, roles, overrides, and scope details.

Example JSON (Create Assignment):
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "roleIds": [
    "987e6543-e21b-12d3-a456-426614174000"
  ],
  "permissionOverrides": [
    {
      "serviceId": "555e4567-e89b-12d3-a456-426614174000",
      "resource": "Student.Profile",
      "level": 3,
      "type": 1
    }
  ],
  "structuralScope": {
    "facultyId": "111e4567-e89b-12d3-a456-426614174000",
    "allFaculties": false,
    "programId": null,
    "allPrograms": true
  },
  "temporalScope": {
    "academicYearId": "222e4567-e89b-12d3-a456-426614174000",
    "semesterId": null,
    "alwaysActive": false
  }
}
```

## 6. Endpoint Documentation

**Get Effective Permissions**
- **Route:** `GET /api/permissions`
- **Purpose:** Retrieves all effective permissions for the currently authenticated user.
- **Authorization:** Requires valid authentication token (implies `ICurrentUser` is populated).
- **Response Body:** `List<PermissionDto>`

**Get Assignment**
- **Route:** `GET /api/permissions/assignment`
- **Purpose:** Retrieves a specific logical grouping of permission assignments based on scope parameters.
- **Query Parameters:** `UserId`, `FacultyId`, `ProgramId`, `AcademicYearId`, `SemesterId`, `AllFaculties`, `AllPrograms`, `AlwaysActive` (Mapped to `GetPermissionAssignmentQueryDto`).
- **Response Body:** `PermissionAssignmentResponse` or `404 Not Found` if no roles/overrides exist for the combination.

**Create Assignment**
- **Route:** `POST /api/permissions`
- **Purpose:** Creates roles and permission overrides for a specific user within defined scopes.
- **Request Body:** `CreatePermissionAssignmentRequest`
- **Validation:** Will return failure if invalid combinations exist (e.g., `AllFaculties = true` AND `FacultyId != null`).
- **Response Body:** `PermissionAssignmentResponse`
- **Note:** Silently ignores attempts to create duplicate role assignments or overrides.

**Update Assignment**
- **Route:** `PUT /api/permissions/assignment`
- **Purpose:** Mutates an existing logical assignment grouping by adding/removing roles and overrides.
- **Request Body:** `UpdatePermissionAssignmentRequest`
- **Validation:** Validates scope combinations similarly to the Create endpoint.
- **Response Body:** `PermissionAssignmentResponse` containing the final computed list of roles and overrides.
- **Note:** Missing entities in "Remove" lists are gracefully ignored. Existing overrides in the "Add" list are updated with the new `ActionLevel`.

## 7. Authorization and Scope Rules

- **Precedence:**
  1. Deny Overrides (`OverrideType.Deny`): Overrules all other rules if its level is `<=` the required level.
  2. Allow Overrides (`OverrideType.Allow`): Overrules Role permissions if the override grants a higher `ActionLevel`.
  3. Role Permissions: Evaluated if no overriding rules take precedence.
- **Levels Hierarchy:** `Delete` (5) > `Open` (4) > `EditClose` (3) > `Insert` (2) > `View` (1) > `None` (0).
- **Wildcard Behavior:** Resource matching checks exact string equality or a `"*"` wildcard.
- **ABAC Behavior (`isClosed` constraint):** For records marked as "closed" (`isClosed = true`), actions like `Insert`, `EditClose`, or `Delete` mandate at least the `Open` (Level 4) permission level, or the specifically required level, whichever is higher (e.g., Delete on closed still strictly needs Level 5).
- **Structural/Temporal Resolution:** Scope combinations evaluate to literal string keys (e.g., "Global" or GUID strings). Role assignments and Overrides only apply if their `Domain`, `Year`, and `Semester` exactly match the requested authorization scope.

## 8. Validation Rules
- **Scope Combination Checks:**
  - Throws `ArgumentException` if `AllFaculties` is `true` AND a `FacultyId` is provided.
  - Throws `ArgumentException` if `AlwaysActive` is `true` AND an `AcademicYearId` or `SemesterId` is provided.
- **Duplicate Handling:** The Create and Update logic filters out duplicates in memory prior to EF Core insertions to prevent SQL unique constraint violations.
- **Conflict Handling:** Handled internally by `AuthorizationEvaluator`. Deny overrides take top precedence.

## 9. Current Technical Debt / Risks
- **Synthetic Grouping Mechanism:** Current implementation couples permission projection grouping to scoped query semantics because no dedicated aggregate identity exists. Assignments are resolved dynamically by querying multi-column scope configurations.
- **Stringly-Typed Scope Persistence:** Structural and temporal scopes (`Domain`, `Year`, `Semester`) are stored as strings. This requires runtime conversion logic (`FacultyId?.ToString() ?? "Global"`) and limits relational integrity at the DB level for scoping rules.
- **Hardcoupled Evaluation Strategy:** `AuthorizationEvaluator` heavily couples its decision making to the exact implementation of in-memory data structures rather than delegating completely to an external policy engine.
- **Navigation Coupling:** Identity permission entities like `StaffPermissionOverride` still maintain direct EF Core navigation properties to `Staff` and `Service`. This creates high coupling across module boundaries, complicating a future transition to strict microservices or separate DB schemas.
- **Delete Logic:** Overrides are removed by scanning lists for matching `ServiceId`, `Resource`, and `Type` values, which might be fragile if multiple identical overrides are mistakenly written to the database.

## 10. Refactor Guidance (WITHOUT CHANGING LOGIC)
- **What can safely move:** The `AuthorizationEvaluator` logic can be decoupled from core and moved into a dedicated Security/Auth module. It does not depend on DB operations directly.
- **What boundaries can split:** The persistence of Role and Permissions assignments can be segregated into an independent "Identity & Access" module. Navigation dependencies (e.g., `Staff` to `StaffRoleAssignment`) should be refactored into ID-only foreign key mappings to support future DbContext separation.
- **What abstractions are stable:** `IAuthorizationEvaluator`, `ActionLevel`, `OverrideType`, and the HTTP API contracts (`PermissionAssignmentResponse` etc.) should remain stable.
- **What MUST remain compatible:** The exact evaluation order (Deny -> Allow Override -> Roles) and ABAC hierarchy (Levels 0-5, Closed-status constraints) must be preserved for backward compatibility. The endpoint routing (`/api/permissions/assignment`) and JSON contract structures must remain untouched.

## 11. Testing Documentation
- The `AuthorizationEvaluator` has detailed testing logic verifying precedence rules, hierarchical Deny constraints, Scope mismatch denials, wildcard resolution, and the specific ABAC constraints modifying closed records.
- **Edge cases:** The application expects requests for assignment groupings to return `404 NotFound` if the specific combination of User and Temporal/Structural scope yields zero rows in the DB.
- **Missing coverage:** Documentation implies tests cover evaluation, but API integration tests validating concurrent duplicate insertion behavior on assignments may need auditing.

## 12. Dependency Documentation
- **Service Dependencies:** The Controllers depend heavily on `IPermissionManagementService`. The Evaluation pipeline relies heavily on `ILoggerService` for recording "Allow/Deny" authorization decisions.
- **Module Dependencies:** The core system implicitly expects Identity configurations to exist. Other feature modules use `IPermissionService` or `IAuthorizationEvaluator` as a foundational abstraction.
- **Request Context Dependencies:** Scoping data is heavily dependent on HTTP context resolving the user (`ICurrentUser`).
- **Infrastructure Assumptions:** Expects Entity Framework Core and relies on Cascade Delete behavior for deleting a `Staff` member or `Role` out of the database.