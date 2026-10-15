# Self_Host_Chat

A full-stack application configured with:
- **Backend (`API/`)**: Clean ASP.NET Core 9 Web API scaffold ready for implementation.
- **Frontend (`Client/`)**: Angular 21 with Tailwind CSS v4, DaisyUI v5 (21 selectable themes), and centralized HTTP Error Handling (Error Interceptor, Toast Service, 404 Not Found, 500 Server Error).

---

## Project Structure

```
Self_Host_Chat/
│
├── API/                             # ASP.NET Core 9 Web API
│   ├── Controllers/                 # API Controllers
│   ├── Entities/                    # Domain Models
│   ├── DTOs/                        # Request/Response DTOs
│   ├── Data/                        # Data context & Migrations
│   ├── Errors/                      # Error models
│   ├── Middleware/                  # Custom Middleware
│   ├── Interface/                   # Service contracts
│   ├── Services/                    # Service implementations
│   ├── Extensions/                  # Service extensions
│   └── Helpers/                     # Utility helpers
│
└── Client/                          # Angular 21 Frontend (Design & Error Handling)
    ├── src/
    │   ├── types/                   # Error models (erro.ts)
    │   ├── core/                    # Error interceptor & Toast service
    │   ├── layout/                  # Shell navbar & 21 DaisyUI themes
    │   ├── shared/error/            # Not-found (404) & Server-error (500) views
    │   └── features/home/           # Home hero banner
```

---

## Quick Start

### 1. Backend (API)
```powershell
cd API
dotnet build
dotnet run
```
*Runs on `https://localhost:5001`.*

### 2. Frontend (Client)
```powershell
cd Client
npm install
npm start
```
*Runs on `http://localhost:4200`.*
