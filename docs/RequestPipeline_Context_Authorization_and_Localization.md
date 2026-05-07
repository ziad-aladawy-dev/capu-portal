# Request Pipeline: Context, Authorization, and Localization

This document describes how request data, user authorization, and localization interact داخل النظام، مع الحفاظ على فصل واضح بين المسؤوليات.

---

# 1. High-Level Flow

```mermaid
flowchart TD
    A[HTTP Request] --> B[Culture Resolution]
    A --> C[RequestContext Middleware]

    B --> D[ILocalizationService]
    C --> E[IRequestContext]

    A --> F[Authentication Middleware]
    F --> G[ICurrentUser]

    G --> H[IUserScope Service]
    
    E --> I[EffectiveScope Builder]
    H --> I

    I --> J[IEffectiveScope]

    J --> K[Application Layer]
    D --> K

    K --> L[Repositories / Queries]
```
2. Responsibilities
Culture (Localization)
Reads Accept-Language
Sets current culture
Used for:
Translations
Formatting (dates, numbers)
RequestContext
Reads headers:
X-Faculty-Id
X-Program-Id
X-Year-Id
X-Semester-Id
Represents requested scope (untrusted)
Authentication
Identifies user
Provides ICurrentUser
UserScope
Loaded from database
Represents allowed scope (trusted)
EffectiveScope
Combines:
Requested scope (RequestContext)
Allowed scope (UserScope)
Produces:
Validated + enforced scope
Used in:
Application logic
Data filtering
3. Core Enforcement Logic
```mermaid
flowchart LR
    A[IRequestContext<br/>Requested Scope] --> C[EffectiveScope Builder]
    B[IUserScope<br/>Allowed Scope] --> C

    C --> D[Validation]
    D --> E[Intersection]

    E --> F[IEffectiveScope<br/>Final Scope Used]
```
Rules:
Request headers are not trusted
User scope is source of truth
Effective scope = intersection + validation
4. Separation of Concerns

```mermaid
flowchart TD
    subgraph Presentation
        A[Culture]
        B[ILocalizationService]
    end

    subgraph Request_Input
        C[IRequestContext]
    end

    subgraph Authorization
        D[IUserScope]
    end

    subgraph Enforcement
        E[IEffectiveScope]
    end

    A --> B
    C --> E
    D --> E
```

Key Principles:
Culture ≠ Data Filtering
RequestContext ≠ Authorization
Only EffectiveScope is used in business logic
5. Execution Order

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant Auth
    participant Context
    participant Scope
    participant App

    Client->>API: HTTP Request

    API->>API: Culture Middleware
    API->>Context: RequestContext Middleware

    API->>Auth: Authenticate User
    Auth-->>API: ICurrentUser

    API->>Scope: Build IUserScope
    API->>Scope: Build IEffectiveScope

    API->>App: Execute Use Case

    App->>Scope: Use IEffectiveScope
    App->>API: Response (localized via culture)
```

6. Final Model
Request
 ├── Culture            → Presentation (language, formatting)
 ├── Requested Scope    → From headers (untrusted)
 ├── User Scope         → From permissions (trusted)
 └── Effective Scope    → Enforced result (used everywhere)


7. Critical Rules
❌ Never use IRequestContext directly in queries
❌ Never trust headers for authorization
✅ Always use IEffectiveScope in business logic
✅ Keep localization completely separate

8. Outcome

This design ensures:

Clear separation of concerns
Secure data access
Scalable filtering model
Maintainable architecture