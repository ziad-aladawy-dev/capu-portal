# Cross-Cutting Concerns Test Documentation 🛠️

This document outlines the test cases for the cross-cutting layers of the CapitalUniversity system, including Authentication, Authorization, Caching, Localization, Logging, and Performance.

## 1. Authentication (`Authentication/`)

Focuses on user identification, credential verification, and token generation.

### Authentication Service
- **Valid Credentials**: Verifies that correct credentials for different roles (Admin, Student) return a valid JWT and the user's authorized scopes/permissions.
- **Invalid Password**: Ensures that a valid identifier with an incorrect password returns an unauthorized response.
- **Missing Identifier**: Validates that non-existent users are handled gracefully.
- **Expired Password**: Tests that expired credentials trigger appropriate security responses.

### Token Service
- **JWT Generation**: Verifies that tokens are created with the correct claims (User ID, Identifier, Role) and adhere to the configured expiry and signing keys.

---

## 2. Authorization (`Authorization/`)

This is the most complex layer, implementing a hierarchical, scope-based permission system.

### Authorization Evaluator
- **Deny Overrides**:
    - **Hierarchical Deny**: A "Deny" override at a parent node (e.g., University) automatically denies access to all child nodes (Faculties, Programs).
    - **Wildcard Support**: Deny overrides using `*` apply to all resources.
- **Role & Assignment**:
    - **Hierarchical Allow**: Permissions granted at a parent node inherit downwards.
    - **Scope Matching**: Ensures permissions only apply if the request's Scope (Structure Node, Year, Semester) matches the user's assignment.
- **Closed Record Logic**:
    - Implements "Lockdown" logic where certain actions (Delete, Edit/Close) on sensitive records require higher "Open" or "Delete" permission levels rather than standard "Full" access.
- **Inheritance Rules**: Parent -> Child (Yes), Child -> Parent (No), Sibling -> Sibling (No).

### Permission Management
- **Effective Permissions**: Tests the logic that aggregates roles and overrides to calculate the final "Effective" permission set for a user in a specific session.

---

## 3. Caching (`Caching/`)

Verified via `RedisCacheServiceTests`.

- **Generic Access**: Tests that `GetAsync<T>` correctly deserializes JSON strings back into strongly-typed objects.
- **Cache Miss**: Ensures `null` is returned when a key does not exist.
- **TTL (Time To Live)**: Verifies that absolute expiration options are correctly passed to the underlying distributed cache provider.
- **Removal**: Confirms that keys can be explicitly evicted.

---

## 4. Localization (`Localization/`)

Verified via `LocalizationServiceTests`.

- **JSON Localization**:
    - **Culture Switching**: Tests that `{"ar": "مرحبا", "en": "Hello"}` returns the correct string based on `ICurrentCultureService`.
    - **Fallback**: Defaults to Arabic if the requested language is missing from the JSON payload.
- **Enum Localization**:
    - **Attributes**: Verifies that `[Localized("Ar", "En")]` attributes on Enum members are correctly resolved.
- **Validation Logging**: Ensures invalid JSON payloads are logged as warnings and return default values rather than crashing.

---

## 5. Logging (`Logging/`)

Verified via `BufferedAppLogger*Tests` (the Mongo audit pipeline is the single logging path).

- **Contextual Enrichment**: Ensures every audit entry captures the actor (`UserId`, `UserName`, `Role`), `IpAddress`, request path/method, and the `CorrelationId` from `HttpContext`.
- **Correlation Propagation**: The correlation id is snapshotted on the request thread so it survives the async hand-off to the Mongo flush worker.
- **Secret Scrubbing**: Free-form messages and metadata are passed through `LogScrubber` before persistence.

---

## 6. Performance (`Performance/`)

Verified via `LocalizationPerformanceTests`.

---

## 7. Cached Authorization & Performance (`Contract.Tests/CachedAuthorizationTests.cs`)

This is a full end-to-end integration test that verifies the caching strategy for the authorization system.

### Strategy
- **Lazy Loading**: User permissions are NOT loaded into the cache during login. Instead, they are lazily loaded on the **first authorized request** that hits the `PermissionHandler`.
- **Hashtable Storage**: Permissions are aggregated from roles and overrides, then transformed into a `HashSet<string>` with composite keys (`Module:Resource:Action`). This ensures **O(1)** lookup time for subsequent requests.
- **Cache Isolation**: Keys are scoped by `UserId`, `AcademicYearId`, and `SemesterId` to ensure correct permissions are applied even if a user switches academic contexts.
- **Auto-Invalidation**: The cache is automatically invalidated when a user's permission assignments are created or updated.

### Test Scenarios
- **Cache Population**: Verifies that the cache is empty after login but contains the correct entries after the first request.
- **Access Control**: Ensures that actions allowed by the role (e.g., `View`) succeed, while actions not granted (e.g., `Insert`) return `403 Forbidden`.
- **Performance Benchmark**: Simulates 100-1000 authorized requests and verifies that the average overhead per request is minimal (under 5ms).

### Results
- **Benchmark**: 100 authorized requests typically complete in **~380ms** (including full HTTP pipeline and in-memory DB overhead), proving the efficiency of the hashtable lookup strategy.

