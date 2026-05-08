# Authentication

## 1. Feature Overview
The Authentication feature manages user identity verification and orchestrates the creation of a centralized login response. This response equips frontend applications with both a lightweight security token (JWT) and an "Authorization Bootstrap Model" that initializes UI structures based on the user's role and scopes.

Major responsibilities include:
- Verifying the user's identity and credentials against multiple potential identity sources (e.g., Staff vs. Students).
- Managing password hashing and verification.
- Generating a lightweight JSON Web Token (JWT).
- Constructing an initialization response containing the allowed operational boundaries (Authorized Scopes) and UI permissions.

Integration points:
- Serves as the first barrier entry point (`/auth/login`) conceptually.
- Relies on an abstraction (`IUserCredentialResolver`) to decouple the authentication flow from specific user databases.
- Acts entirely independent of the backend authorization enforcement, focusing only on verifying *who* the user is, rather than *what* they are doing.

## 2. Architecture Placement
The feature currently lives centrally inside the core architectural layers:
- **Core Abstractions (`src/Core/Abstractions/Auth/Authentication`):** Defines the contracts like `IAuthenticationService`, `ITokenService`, `IUserCredentialResolver`, `IPasswordHasher`, and `IAuthorizationResponseBuilder`. Defines the request/response DTOs (`LoginRequestDto`, `LoginResponseDto`).
- **Core Application (`src/Core/Application/Auth/Authentication`):** Implements the logic in `AuthenticationService`, `TokenService` and `PasswordHasher`.

Dependencies:
- `AuthenticationService` depends on several interfaces (`IUserCredentialResolver`, `IPasswordHasher`, `IAuthorizationResponseBuilder`, `ITokenService`), avoiding hard-coupling to EF Core DbContexts or specific user entities directly.
- The controller exposing this feature is currently absent or conceptually intended to sit at the API layer.
- Uses configuration settings (`IConfiguration`) inside `TokenService` for JWT creation.

## 3. Data Flow

**Authentication Request Flow:**
```text
Client (POST /auth/login)
→ API Controller (Conceptual)
→ AuthenticationService.AuthenticateAsync(LoginRequestDto)
   → IUserCredentialResolver (Finds User by Identifier)
   → Check Expiry (DateTime.UtcNow > PasswordExpiry)
   → IPasswordHasher.VerifyHashedPassword()
   → ITokenService.GenerateToken(IUserCredential)
   → IAuthorizationResponseBuilder.BuildAsync() (Builds UI Permissions & Scopes)
→ LoginResponseDto
→ Client
```

## 4. Entity and Persistence Mapping
The Authentication feature deliberately abstracts persistence behind the `IUserCredential` and `IUserCredentialResolver` contracts.

- `IUserCredential`: A read-only projection containing:
  - `Id`
  - `Identifier` (National ID or Email)
  - `PasswordHash`
  - `PasswordExpiry`
  - `Role`
  - Extracted attributes: `UniAttribute`, `FacultyAttribute`, `DepartmentAttribute`.

**Persistence Semantics:**
The system is explicitly designed to support multiple identity sources (Staff, Students) without tightly coupling the `AuthenticationService` to specific Identity EF Core tables. The actual lookup logic resides inside the implementation of `IUserCredentialResolver`.

## 5. DTOs and Contracts

**Request DTO:**
`LoginRequestDto`:
- `Identifier`: The username, email, or national ID.
- `Password`: The raw password.

**Response DTO:**
`LoginResponseDto`: The "Authorization Bootstrap Model" meant entirely for the frontend context.
- `User`: Basic user profile (`Id`, `Name`, `Email`) + `Attributes` (`Uni`, `Faculty`, `Department`).
- `Token`: The generated JWT string.
- `AuthorizedScopes`: `AllowedFacultyIds`, `AllowedProgramIds`, `AllowedAcademicYearIds`, `AllowedSemesterIds`.
- `Permissions`: A list of strings mapping to UI feature flags, in the format `Module.Resource.Action` (e.g., `Student.Profile.Edit`).
- `ActiveScope`: The structural and temporal context currently active for the user session.

Example JSON (Login Response):
```json
{
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "Jane Doe",
    "email": "jane.doe@university.edu",
    "attributes": {
      "uni": "Capital",
      "faculty": "Engineering",
      "department": "Software"
    }
  },
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "authorizedScopes": {
    "allowedFacultyIds": ["111e4567-e89b-12d3-a456-426614174000"],
    "allowedProgramIds": [],
    "allowedAcademicYearIds": [],
    "allowedSemesterIds": []
  },
  "permissions": [
    {
      "key": "Student.Profile.Edit",
      "module": "Student",
      "resource": "Profile",
      "action": "Edit"
    }
  ],
  "activeScope": {
    "structural": {
      "facultyId": "111e4567-e89b-12d3-a456-426614174000",
      "programId": null
    },
    "temporal": {
      "academicYearId": "222e4567-e89b-12d3-a456-426614174000",
      "semesterId": null
    }
  }
}
```

## 6. Endpoint Documentation

*(Note: The explicit `AuthController` is not currently present in the API project, but the conceptual contract is defined as follows based on the service signature and architecture documents).*

**Authenticate User**
- **Route:** `POST /auth/login` (Conceptual)
- **Purpose:** Validates credentials and returns a JWT along with the UI initialization context.
- **Request Body:** `LoginRequestDto`
- **Validation:**
  - Fails if `Identifier` or `Password` is missing or empty.
  - Fails if user cannot be resolved.
  - Fails if `PasswordExpiry` is passed.
  - Fails if password hash verification fails.
- **Response Body:** `LoginResponseDto` (or `401 Unauthorized`/`400 Bad Request` depending on implementation).

## 7. Authorization and Scope Rules
This feature does *not* perform authorization. It *assembles* the authorization data required by the frontend client to initialize its UI guards.

- **JWT Claims:** The token is kept strictly lightweight. It includes `Id`, `NationalId`, `Role`, and a `Jti`. It specifically avoids packing permissions or scope data into the token to prevent token bloat.
- **Frontend Scopes:** The backend passes down `AuthorizedScopes` and `Permissions` via the `LoginResponseDto` exactly once at login, trusting the backend pipelines to physically enforce those rules on subsequent requests.

## 8. Validation Rules
- Required fields: `Identifier` and `Password` must not be null or whitespace.
- Expiration check: The `PasswordExpiry` on the credential must be greater than `DateTime.UtcNow`.

## 9. Current Technical Debt / Risks
- **Missing API Controller:** The `AuthController` implementation to expose `/auth/login` is missing from `src/API/Controllers`. The `AuthenticationService` exists but is disconnected from the HTTP perimeter.
- **Hardcoded JWT Defaults:** The `TokenService` uses hardcoded fallback string values for `Jwt:Secret`, `Jwt:Issuer`, and `Jwt:Audience`. If configuration is missing, this results in insecure defaults being used in production.
- **Limited Granular Errors:** `AuthenticationService.AuthenticateAsync` currently returns `null` for *any* failure (e.g., missing input, not found, expired, invalid password), losing the ability to tell the client *why* login failed (e.g., throwing a `PasswordExpiredException`).

## 10. Refactor Guidance (WITHOUT CHANGING LOGIC)
- **What can safely move:** The entire `Core.Application.Auth.Authentication` folder can be extracted into an `Identity` or `Security` module. Since it depends strictly on interfaces (`IUserCredentialResolver`), it is perfectly decoupled from entity persistence.
- **What boundaries can split:** The generation of the authorization bootstrap data (`IAuthorizationResponseBuilder`) could be shifted downstream or requested by the frontend via a separate endpoint (`GET /auth/me`), which would slim down the core authentication loop.
- **What abstractions are stable:** The separation of `IUserCredentialResolver` and `IAuthenticationService` is very stable and adheres well to the Clean Architecture boundaries.
- **What MUST remain compatible:** The `LoginResponseDto` shape must remain structurally identical to support the frontend's initialization and routing logic. The JWT must remain lightweight and avoid holding permission collections.

## 11. Testing Documentation
- **Expected scenarios:** Tests should cover successful logins, empty request parameters, non-existent identifiers, expired passwords, and incorrect passwords.
- **Missing coverage:** The API controller integration test cannot be written yet since the endpoint is missing.

## 12. Dependency Documentation
- **Service Dependencies:** Orchestrates dependencies across `IUserCredentialResolver`, `IPasswordHasher`, `IAuthorizationResponseBuilder`, and `ITokenService`.
- **Infrastructure Assumptions:** The application expects JWT configuration keys (`Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`) to exist in the `appsettings.json` or environment variables to override the insecure defaults.