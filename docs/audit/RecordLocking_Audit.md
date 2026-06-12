# Record Locking Audit Report

**Scope:** System-wide audit of the "Closable" lifecycle (Finalization/Approval/Submission) to determine where records should be CLOSED to prevent modifications.
**Feature Design:**
- **Status Flag**: `IsClosed` / `ClosedAt`.
- **Domain Guard**: `EnsureMutable()` (throws `ConflictException` if closed).
- **Logical APIs**: Dedicated `CloseRecord` and `OpenRecord` endpoints/service methods (not just a status flip in a general update).
- **Authorization**:
    - `EditClose` permission: Usually for staff/admins to finalize a record.
    - `Open` permission: High-level admin permission to "unlock" a finalized record for modification.
    - **Student Lifecycle**: Students need to lock their requests upon submission.

---

## 1. Classification

### REQUIRED

| Entity | Reason | Current Status | Gaps |
| :--- | :--- | :--- | :--- |
| **Academic Plans** | Structural curriculum data. Must be finalized before used for registration. | **Implemented** | None. Has endpoints + domain guards. |
| **Courses** | Catalog base data. Finalized once verified. | **Implemented** | None. |
| **Academic Years** | Temporal structural data. | **Implemented** | None. |
| **Semesters** | Temporal structural data. | **Implemented** | None. |
| **Course Offerings** | Runtime availability. Finalized before registration opens. | **Implemented** | None. |
| **Schedule Slots** | Timetable rows. Finalized before registration opens. | **Implemented** | None. |
| **Student Service Requests** | Requests from students. Must be locked by students upon submission and by staff upon completion. | **Partial** | Missing `IsClosed` domain pattern. Student-driven lock on `Submit` needs dedicated API/logical handling. |
| **Payment Orders** | Financial transaction attempts. Must be locked post-payment or post-expiry. | **Partial** | Missing `IsClosed` domain pattern. |
| **Student Fees** | Financial obligations. Once `Paid`, must be immutable. | **Missing** | No locking pattern implemented. |
| **Student Profile Records** | Sensitive data. Once `VerifiedBy` staff, should be locked. | **Missing** | No locking pattern implemented. |

### OPTIONAL

| Entity | Reason |
| :--- | :--- |
| **Staff / Student Profiles** | Locked for investigation or audit purposes. |
| **University Structure (Nodes)**| Locked between restructuring phases. |

### NOT APPLICABLE

| Entity | Reason |
| :--- | :--- |
| **Registered Courses / Grades** | Read-only snapshots synced from external systems. |
| **Treasury Receipts** | Read-only reference data from external system. |
| **Audit Logs / Notifications** | Append-only transactional records. |

---

## 2. Key Findings & Logical API Requirements

### Logical API Constraint
A "Close/Open" feature is not just a status flag. It requires **dedicated logical APIs** to ensure the business transition is atomic and properly authorized.
- `POST /id/close-record`: Applies the lock.
- `POST /id/open-record`: Removes the lock (Admin only).

### Student-Driven Locking
For **Student Service Requests**, the "Submit" action should logically "Close" the record for the student. If staff request "More Info", the record is "Opened" for the student. Once staff "Complete" the request, it is "Closed" for everyone.

### Verification of Implemented Entities
The following entities have been verified to have **both** the domain guard `EnsureMutable()` and the logical APIs (`CloseRecord`/`OpenRecord`) with correct permission gating:
- `AcademicPlan`
- `AcademicYear`
- `Semester`
- `Course`
- `CourseOffering`
- `ScheduleSlot`

---

## 3. Gap Analysis & Production Impact

| Gap | Description | Impact |
| :--- | :--- | :--- |
| **Student Request Locking** | No formal `IsClosed` lock post-submission. | **High**: Students could theoretically modify data after submission but before staff review if the status check is bypassed or misconfigured. |
| **Financial Immutable Fees** | Paid fees can be modified without a formal "Reopen" audit trail. | **High**: Risk to financial integrity and auditability. |
| **Verified Data Tampering** | Verified profile records can be edited without losing "Verified" status. | **Medium**: Undermines the value of the verification process. |
