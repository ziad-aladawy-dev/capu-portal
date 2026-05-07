# Authentication Model

## Overview

The authentication system is designed as a centralized orchestration flow responsible for:

* validating credentials
* resolving user identity source
* generating JWT tokens
* constructing frontend authorization bootstrap data

The system supports:

* Students
* Admins

through separate credential sources while exposing a unified authentication contract.

---

# Authentication Flow

```text
POST /auth/login
    ↓
AuthController
    ↓
AuthenticationService
    ├─ IUserCredentialResolver
    ├─ IPasswordHasher
    ├─ IAuthorizationResponseBuilder
    └─ ITokenService
    ↓
LoginResponseDto
```

---

# Core Design Principles

## 1. Authentication != Authorization

Authentication verifies:

* identity
* credentials

Authorization determines:

* accessible scopes
* allowed actions
* effective operational boundaries

The login response contains an authorization bootstrap model for frontend initialization only.

The backend remains the final authorization source of truth.

---

## 2. JWT Responsibilities

JWT tokens contain only lightweight session context.

### Included Claims

* UserId
* Role
* SessionVersion
* Optional minimal active context

### Excluded Claims

* permission collections
* authorization graphs
* UI data
* scope trees

---

# User Source Resolution

The system supports multiple credential sources.

Examples:

* Student credentials
* Admin credentials

`IUserCredentialResolver` is responsible for:

* locating the user
* identifying the source type
* returning a unified credential result

This prevents authentication logic from being tightly coupled to database structure.

---

# Authorization Bootstrap Model

After successful authentication, the system builds a frontend-friendly authorization response.

The response contains:

## Authorized Scopes

Represents the allowed operational boundaries for the authenticated user.

Examples:

* faculties
* programs
* semesters
* academic years

---

## Permissions

Permissions follow the format:

```text
Module.Resource.Action
```

Example:

```text
Student.Profile.Edit
```

Permissions are intended for:

* frontend guards
* UI rendering
* feature visibility

The backend still validates authorization independently.

---

## Active Scope

Represents the initial operational context.

The active scope is divided into:

### Structural Context

* faculty
* program

### Temporal Context

* academic year
* semester

---

# Request Context

The system is designed to support request-scoped operational context.

Future requests may provide:

* faculty selection
* program selection
* scope selection

through request headers.

This allows:

* lightweight JWTs
* dynamic scope switching
* reduced token regeneration

---

# Service Responsibilities

## AuthenticationService

Orchestrates authentication flow only.

Responsibilities:

* credential validation
* password verification
* authorization response coordination
* token generation coordination

---

## IUserCredentialResolver

Handles:

* user source resolution
* credential lookup

---

## IPasswordHasher

Handles:

* password hashing
* password verification

---

## IAuthorizationResponseBuilder

Builds:

* permissions
* authorized scopes
* active scope
* login authorization DTO

---

## ITokenService

Handles:

* JWT generation
* token expiration
* token signing

---

# Architectural Constraints

The authentication system intentionally avoids:

* CQRS
* MediatR
* event buses
* distributed orchestration
* runtime plugin discovery

The design prioritizes:

* simplicity
* maintainability
* explicit orchestration
* predictable debugging
