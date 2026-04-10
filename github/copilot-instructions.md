# Senior .NET 10 Architect Rules

## Tech Stack
- Framework: .NET 10 / C# 14.
- Validation: FluentValidation only.
- Test Framework: xUnit with AutoFixture and Moq.
- API Style: Minimal APIs or Clean Controllers.
- Result Pattern: Use FluentResults. Never throw exceptions for business logic.
- Logging: Use Serilog with structured logging. Implement a Global Exception Middleware.
- Unit Testing: Use xUnit and FluentAssertions.

#

## Logic & Clean Code
- **Method Limit:** Max 20 lines. If longer, refactor to private methods or Specifications.
- **Guard Clauses:** Always use "Invert If". Use `ThrowIfNull` or `ThrowIfEmpty` helpers.
- **Error Handling:** Use the Result Pattern; do not throw exceptions for expected failures.
- **Directives:** Use file-scoped namespaces and global usings where possible.

## File & Folder Structure
- **Strict Separation:** Files must contain only one Class or Interface.
- **Organization:** Use the following structure for every feature:
  - `Features/{FeatureName}/Requests/`
  - `Features/{FeatureName}/Responses/`
  - `Features/{FeatureName}/Validators/`
  - `Features/{FeatureName}/Interfaces/`

## Verification
- Before finishing a task, verify that all new interfaces have a corresponding Unit Test file in the `.Tests` project following the AAA (Arrange, Act, Assert) pattern.


## Error Handling & Results
- **No Exceptions:** Never throw exceptions for business logic or validation errors.
- **FluentResults:** Use the `Result` or `Result<T>` pattern for all service and controller methods.
- **Mapping:** In the API layer, map `Result` objects to appropriate HTTP status codes (200, 400, 404) via a base controller or helper.

## Logging & Observability
- **Serilog:** Use Serilog as the primary logging provider. 
- **Structured Logging:** Always use structured logging with message templates (e.g., `LogInformation("User {UserId} logged in", id)`).
- **Middleware:** Use a custom Middleware for global exception handling and request/response logging.

## Middleware Rules
- Create a `GlobalExceptionMiddleware` to catch unhandled errors and log them via Serilog.
- Create a `RequestLoggingMiddleware` to log execution time and status codes for every request.


## API Security Standards
- **OWASP Compliance:** Always implement Rate Limiting and prevent Mass Assignment by using separate /Requests DTOs (already enforced).
- **Security Headers:** Always include a middleware that adds headers: `Content-Security-Policy`, `X-Content-Type-Options`, and `Strict-Transport-Security`.
- **Validation:** Use FluentValidation to strictly enforce "White-listing" (reject any input that doesn't strictly match the expected pattern).

## Data Integrity
- **SQL Injection:** Always use Entity Framework Core parameters; never use string concatenation for queries.
- **JSON Security:** Set `System.Text.Json` to `AllowDuplicateProperties = false` to prevent JSON Collision attacks.


## Security & Identity
- **Cryptography:** For sensitive data, use .NET 10 Post-Quantum Cryptography (PQC) algorithms (e.g., ML-DSA or ML-KEM).
- **Sensitive Data:** Never log Passwords, Tokens, or PII. Use the [SensitiveData] attribute or a masking library.
- **Secrets:** Never hardcode strings. Use `user-secrets` for local dev and Azure Key Vault/Environment Variables for prod.

Database & Entity Framework Core 10
- **Primary Keys:** Always use UUIDv7 for PostgreSQL IDs.
- **Performance:** Use `AsNoTracking()` for read-only queries. 
- **Bulk Operations:** Use `ExecuteUpdateAsync` and `ExecuteDeleteAsync` for mass changes.
- **Mapping:** Use PostgreSQL JSONB for flexible metadata columns.
- **Entity Configurations:** Always use FluentConfigurations for entity configurations and never use data annotations.


Rendering & State
- **Render Modes:** Default to `InteractiveAuto` for the best balance of speed and interactivity. Use `Static SSR` for pure content pages.
- **State Persistence:** Use the `[PersistentState]` attribute to ensure user data survives circuit reconnections.
- **Navigation:** Always use the `NavigationManager` with the .NET 10 enhanced 404 handling.

## 2. Component Architecture
- **Purity:** Keep components logic-light. Move business logic into Services and call them from `@code` blocks.
- **Validation:** Always use the .NET 10 `<FluentValidationValidator />` for forms. Support nested validation for complex DTOs.
- **JS Interop:** Use the native .NET 10 direct object property access (e.g., `GetPropertyAsync`) instead of writing custom JS wrappers.

## 3. UI Security
- **Authentication:** Prefer **Passkeys (WebAuthn)**. Use the built-in .NET 10 identity scaffolding for passwordless login.
- **XSS Prevention:** Never use `MarkupString` with raw user input.
- **Sensitive Data:** Use `ResourcePreloader` for preloading framework assets and ensure CSP-compliant scripts are used.