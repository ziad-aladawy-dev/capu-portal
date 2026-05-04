# CapitalUniversity 🎓

Welcome to **CapitalUniversity**, a comprehensive enterprise-grade Student Portal designed to manage the academic lifecycle of a university. The platform caters to students, instructors, student affairs personnel, and administrators through a highly modular and scalable architecture.

---

## 🏗️ Architecture Overview

CapitalUniversity is built using a **Modular Monolith** pattern on the backend and a corresponding **Modular Frontend** architecture. This ensures high cohesion, loose coupling between domains, and easy extensibility through plug-in modules.

```
CapitalUniversity/
├── src/                  # Backend applications and modules
│   ├── 1.API/            # Main entry point and host configuration
│   ├── 2.Core/           # Domain-centric core (Abstractions, Domain, Application, Infrastructure)
│   ├── 3.SharedKernel/   # Building blocks and utilities
│   ├── 4.Modules/        # Independent plug-in modules (e.g., Student, Enrollment, Complaints)
│   └── 5.Application/    # Cross-module orchestration, sagas, and read models
├── tests/                # Unit, integration, contract, and architecture tests
├── frontend/             # Modular frontend application (Vue/Vite-based)
├── docs/                 # System architecture, standards, and guides
└── deployment/           # Docker and Nginx configuration
```

---

## 🚀 Key Features

* **Authentication & Authorization:** Role-based access control (RBAC) ensuring secure separation between Admins, Student Affairs, Instructors, and Students.
* **University Structure & Academic Calendar:** Centralized management of faculties, departments, academic years, and semesters.
* **Student Management:** Complete profiles, enrollment, status tracking, and academic progress monitoring.
* **Complaint & Support Management:** Ticketing and resolution tracking between students and staff.
* **Notifications & Auditing:** Multi-channel notification system (Email, SMS, In-App) and comprehensive system-wide logging.

---

## 🛠️ Technology Stack

* **Backend:** .NET 9+ (C#), ASP.NET Core, Entity Framework Core, Redis, and MediatR.
* **Frontend:** Vue 3, Vite, Pinia, Vue Router, and Axios.
* **Database:** PostgreSQL / SQL Server.
* **Containerization:** Docker & Docker Compose.

---

## 🧪 Testing

The solution is backed by a robust testing strategy to ensure compliance and architectural integrity:

* **Unit Tests:** Covers core domain logic and application use cases.
* **Integration Tests:** Ensures correct interaction with the database and external services.
* **Contract Tests:** Enforces `IModule` and abstraction consistency across modules.
* **Architecture Tests:** Automated checks to maintain clean dependencies and architectural constraints.

---

## 📦 Getting Started

### Prerequisites

* [.NET 9 SDK](https://dotnet.microsoft.com/)
* [Node.js](https://nodejs.org/) (v18+)
* [Docker](https://www.docker.com/)

### Local Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-org/CapitalUniversity.git
   cd CapitalUniversity
   ```

2. **Restore Dependencies:**
   ```bash
   dotnet restore CapitalUniversity.sln
   cd frontend && npm install
   ```

3. **Run the Application:**
   * **API:** `dotnet run --project src/1.API/CapitalUniversity.API.csproj`
   * **Frontend:** `npm run dev`

---

## 📚 Documentation

For further information on development standards, consult the `docs/` folder:

* `docs/ARCHITECTURE.md` — Deep dive into the modular architecture.
* `docs/MODULE_DEVELOPER_GUIDE.md` — Step-by-step instructions for adding new modules.
* `docs/CODING_STANDARDS.md` — Formatting and architecture enforcement rules.
