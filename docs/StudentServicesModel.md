# Student Services Module Specification

## Overview

Implement a fully modular Student Services module for the university portal system.

This module allows faculty staff to manage university services and allows students to submit requests for those services.

The system must support:

- Dynamic service forms
- Configurable workflows
- Role-based staff processing
- File uploads
- Fee integration with the already existing Fees module
- Logging integration with the already existing Logging module
- CQRS architecture
- Soft delete
- Pagination/filtering/sorting

Do NOT reimplement authentication, authorization, logging, or fee payment logic.

The project already contains:
- Authentication & authorization
- Logging module
- Fees module
- Modular architecture
- CQRS patterns
- EF Core
- SQL Server
- Mapperly

You must inspect the existing architecture and follow the existing project conventions instead of inventing new architectural patterns.

---

# Functional Requirements

## 1. Service Management

Faculty staff members can:

- Create services
- Update services
- Soft delete services
- Enable/disable services
- Configure workflows
- Configure required documents
- Configure dynamic fields
- Configure fee requirements
- Assign allowed processing roles

Example services:
- Transcript Request
- Enrollment Certificate
- ID Replacement
- Graduation Clearance

---

## 2. Student Service Requests

Students can:

- View available services
- View service details
- Submit requests
- Upload required files
- Track request status
- Cancel requests only before staff processing begins

Students cannot:
- Submit incomplete forms
- Skip required fields
- Skip required files
- Process requests

---

## 3. Workflow System

Services must support configurable workflows.

Example statuses:
- Draft
- Submitted
- WaitingPayment
- UnderReview
- Approved
- Rejected
- Completed
- Cancelled

Workflows should be extensible and not hardcoded.

The workflow must support:
- Role-based transitions
- Validation before transition
- Automatic transitions after payment completion

---

## 4. Fee Integration

The Fees module already exists.

DO NOT:
- Implement payment logic
- Implement payment gateways
- Duplicate fee entities

Instead:
- Integrate with the existing Fees module
- Create fee obligations when required
- Move request status to `WaitingPayment`
- Continue processing after payment confirmation

The implementation should be event-driven if the existing architecture already supports domain/integration events.

---

## 5. Dynamic Form Fields

Services must support configurable custom fields.

Example:
Transcript Request:
- Language
- NumberOfCopies

ID Replacement:
- Reason
- PoliceReportNumber

Supported field types:
- Text
- Number
- Date
- Boolean
- Dropdown
- File
- Multi-line text

Fields must support:
- Required validation
- Min/max length
- Min/max numeric value
- Allowed file types
- Allowed dropdown values

---

## 6. File Uploads

Requests must support required document uploads.

Examples:
- National ID
- Proof of enrollment
- Medical report

Requirements:
- Validate required documents
- Validate file extensions
- Validate maximum file size
- Store file metadata in database
- Follow existing file storage approach in the project

Do NOT invent a new storage mechanism if one already exists.

---

## 7. Staff Processing

Staff members should only see requests relevant to their role/job title.

Examples:
- Registrar staff process transcript requests
- Financial staff process financial approvals

The implementation must integrate with the existing authorization system.

Do NOT implement custom authorization logic from scratch.

---

## 8. Logging Integration

The Logging module already exists.

Integrate all critical actions with logging:
- Service creation
- Service updates
- Service deletion
- Request submission
- Status changes
- Request cancellation
- Staff approvals/rejections

Use the existing logging abstractions and conventions.

---

# Technical Requirements

## Architecture

You must inspect the existing architecture and follow its conventions.

The implementation should align with:
- Existing modular structure
- Existing CQRS implementation
- Existing dependency injection patterns
- Existing repository abstractions
- Existing result/error handling patterns
- Existing validation approach
- Existing domain patterns

Do not introduce unnecessary architectural changes.

---

# Required Deliverables

Implement all required layers for the module.

---

# Domain Design

Create complete domain models for:

## Core Entities

### StudentService
Represents a configurable university service.

Suggested properties:
- Id
- Name
- Description
- IsActive
- RequiresPayment
- FeeTypeId or FeeReference
- ProcessingRoleId / AllowedRoles
- WorkflowDefinitionId
- EstimatedProcessingDays
- Soft delete fields
- Audit fields

---

### StudentServiceRequest
Represents a student request instance.

Suggested properties:
- Id
- StudentId
- StudentServiceId
- CurrentStatus
- SubmittedAt
- ProcessedAt
- AssignedStaffId
- CancellationReason
- PaymentReferenceId
- Soft delete fields
- Audit fields

---

### ServiceFieldDefinition
Represents configurable fields for a service.

Suggested properties:
- Id
- StudentServiceId
- Name
- Label
- FieldType
- IsRequired
- ValidationRules
- DisplayOrder
- DropdownValues

---

### ServiceFieldValue
Stores submitted values.

Suggested properties:
- Id
- StudentServiceRequestId
- FieldDefinitionId
- Value

---

### ServiceDocumentDefinition
Defines required documents.

Suggested properties:
- Id
- StudentServiceId
- Name
- AllowedExtensions
- MaxFileSize
- IsRequired

---

### ServiceDocumentSubmission
Represents uploaded files.

Suggested properties:
- Id
- StudentServiceRequestId
- DocumentDefinitionId
- FileName
- StoredFileName
- FilePath
- ContentType
- FileSize

---

### WorkflowDefinition
Represents service workflow configuration.

---

### WorkflowState
Represents statuses and transitions.

---

# Enums

Implement suitable enums.

Examples:
- ServiceRequestStatus
- DynamicFieldType
- WorkflowTransitionType

Use the existing enum conventions in the project.

---

# CQRS Implementation

Implement complete CQRS support.

---

## Commands

Suggested commands:

### Services
- CreateStudentServiceCommand
- UpdateStudentServiceCommand
- DeleteStudentServiceCommand
- ToggleStudentServiceStatusCommand

### Requests
- SubmitStudentServiceRequestCommand
- CancelStudentServiceRequestCommand
- ApproveStudentServiceRequestCommand
- RejectStudentServiceRequestCommand
- MoveRequestWorkflowStateCommand

---

## Queries

Suggested queries:

### Services
- GetAllStudentServicesQuery
- GetStudentServiceByIdQuery
- GetAvailableStudentServicesQuery

### Requests
- GetStudentRequestByIdQuery
- GetStudentRequestsQuery
- GetStaffAssignedRequestsQuery
- GetPendingRequestsQuery

All list queries must support:
- Pagination
- Filtering
- Sorting

Default sorting:
- Oldest first (in pending requests only)
- Otherwise its latest first

Allow configurable sorting direction.

---

# Validation

Implement FluentValidation validators for:
- Commands
- File uploads
- Dynamic field validation
- Workflow transitions

Validation requirements:
- Required fields
- Required files
- Invalid transitions
- Unauthorized processing
- Invalid dropdown values
- File restrictions

---

# DTOs

Implement DTOs for:
- Service details
- Service listings
- Request details
- Request summaries
- Workflow transitions
- Dynamic fields
- File metadata

Use Mapperly for mappings.

Follow existing mapper conventions.

---

# Repositories

Implement repository abstractions and implementations according to the existing project style.

Suggested repositories:
- IStudentServiceRepository
- IStudentServiceRequestRepository
- IWorkflowRepository

Do not duplicate generic repository patterns if they already exist.

---

# API Endpoints

Implement REST APIs following the existing API style.

---

## Student APIs

### Services
- GET /api/student-services
- GET /api/student-services/{id}

### Requests
- POST /api/student-service-requests
- GET /api/student-service-requests
- GET /api/student-service-requests/{id}
- POST /api/student-service-requests/{id}/cancel

---

## Staff APIs

### Service Management
- POST /api/admin/student-services
- PUT /api/admin/student-services/{id}
- DELETE /api/admin/student-services/{id}

### Request Processing
- GET /api/admin/student-service-requests
- POST /api/admin/student-service-requests/{id}/approve
- POST /api/admin/student-service-requests/{id}/reject
- POST /api/admin/student-service-requests/{id}/transition

---

# Database Requirements

Use EF Core configurations.

Implement:
- Proper relationships
- Cascade behavior where appropriate
- Soft delete query filters

Add migrations following the existing migration conventions.

---

# Filtering & Pagination

All listing endpoints must support:
- Page number
- Page size
- Filtering
- Search
- Sorting

Examples:
- Status filtering
- Service filtering
- Date filtering
- Student filtering
- Staff filtering

---

# Security Requirements

Integrate with the existing authentication & authorization system.

Requirements:
- Students can only access their own requests
- Staff can only process authorized services
- Unauthorized workflow transitions must fail

Do not create a new authorization framework.

---

# Event Flow

Support event-driven behavior where applicable.

Example flow:
1. Student submits request
2. Request created
3. If payment required:
   - Create fee obligation through Fees module
   - Move request to WaitingPayment
4. Payment completed
5. Request moves to UnderReview
6. Staff processes request
7. Request completed

Reuse existing event infrastructure if available.

---

# Additional Requirements

## Soft Delete
All major entities should support soft delete.

---

## Auditing
Reuse existing audit infrastructure if present.

---

## Error Handling
Follow existing project result/error patterns.

Do not introduce inconsistent exception handling styles.

---

# Important Constraints

DO NOT:
- Reimplement authentication
- Reimplement authorization
- Reimplement payment systems
- Reimplement logging
- Introduce unnecessary frameworks
- Hardcode workflows
- Hardcode service forms

DO:
- Follow existing architecture
- Reuse existing infrastructure
- Keep the module extensible
- Keep the module modular
- Keep workflows configurable
- Keep validation centralized

---

# Expected Outcome

The final implementation should provide:
- A scalable student services system
- Extensible workflows
- Dynamic forms
- Secure request processing
- Fee integration
- Logging integration
- Clean CQRS architecture
- Production-ready APIs
- Maintainable domain design