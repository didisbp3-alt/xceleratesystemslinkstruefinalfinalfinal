# XcelerateLinks – Project Documentation

This README documents the XcelerateLinks platform architecture, logic, and conventions. It replaces inline code comments that were extracted during a clean-up pass.

---

## Table of Contents
1. [Solution Structure](#solution-structure)
2. [Technology Stack](#technology-stack)
3. [Authorization Model](#authorization-model)
4. [API Project (APIPSI16)](#api-project-apipsi16)
   - [Authentication & Sessions](#authentication--sessions)
   - [User Management](#user-management)
   - [Employer Workflow](#employer-workflow)
   - [Opportunities & Matching](#opportunities--matching)
   - [Job Applications Pipeline](#job-applications-pipeline)
   - [Chat & Real-time Presence](#chat--real-time-presence)
   - [Skills & Validation](#skills--validation)
   - [Notifications](#notifications)
   - [Company Management](#company-management)
   - [Ratings](#ratings)
   - [Subscriptions](#subscriptions)
5. [MVC Project (XcelerateLinks)](#mvc-project-xceleratelinks)
6. [Database Migrations](#database-migrations)
7. [Key Services](#key-services)

---

## Solution Structure

```
APIPSI16.sln
├── APIPSI16/                  ← ASP.NET Core Web API (REST + SignalR hub)
│   ├── Controllers/           ← REST API controllers
│   ├── Data/                  ← EF Core DbContext
│   ├── Hubs/                  ← SignalR hub (ChatHub)
│   ├── Migrations/            ← EF Core database migrations
│   ├── Models/                ← Entity models + DTOs
│   ├── Services/              ← Business-logic services
│   ├── Middleware/            ← Session validation middleware
│   └── Filters/               ← Swagger file-upload filter
├── XcelerateLinks/            ← ASP.NET Core MVC front-end
│   ├── Controllers/           ← MVC controllers (proxy to API)
│   ├── Views/                 ← Razor views
│   └── wwwroot/               ← Static assets (CSS, JS, uploads)
└── XcelerateLinks_DTOs/       ← Shared DTO library
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend API | ASP.NET Core 8, Entity Framework Core (SQL Server) |
| Front-end | ASP.NET Core MVC (Razor Views), Bootstrap 5 |
| Real-time | SignalR WebSockets |
| Database | SQL Server (hosted on `sql.bsite.net`) |
| Authentication | JWT Bearer tokens (HS256), stored in HttpOnly cookies |
| File Storage | Local disk (`wwwroot/uploads/`) via `IFileStorageService` |
| Password Hashing | ASP.NET Core `IPasswordHasher<User>` |
| API Documentation | Swagger / OpenAPI (Swashbuckle) |

---

## Authorization Model

| Role value | Name | Access |
|---|---|---|
| `0` | Admin | Full system access |
| `1` | Regular User / Job Seeker | Own profile, apply to opportunities, connect |
| `2` | Employer | Own company opportunities, pipeline management, approve/reject employer requests (if CompanyAdmin) |
| `3` | Pending Employer | Awaiting admin/company-admin approval |

**Company Member roles** (stored in `CompanyMembers.Role`):

| Value | Name | Permissions |
|---|---|---|
| `0` | Pending | Not yet activated |
| `1` | Recruiter | Screen / interview / reject applications |
| `2` | HR Manager | All recruiter actions + make offers |
| `3` | Company Admin | All HR manager actions + hire, manage members, approve/reject employer requests |

---

## API Project (APIPSI16)

### Authentication & Sessions

- **`POST /api/auth/login`** – Validates credentials, creates a `Session` record, returns a JWT.
- **`POST /api/auth/register`** – Creates a new user with a hashed password (using `IPasswordHasher<User>`).
- **`POST /api/auth/logout`** – Invalidates the current session.
- JWT is signed with a Base64-encoded key from configuration (`Jwt:Key`). Issuer and audience are validated.
- The `SessionValidationMiddleware` checks that the session token in every request is still valid (not expired or revoked).
- Sessions table (`Sessions`) tracks active tokens with expiry times.

### User Management

**Key endpoints (`/api/users`):**

- `GET /api/users` – Admin: all users. With query params (`jobPreference`, `nationality`): Admin or Employer filtered list.
- `GET /api/users/{id}` – Own profile (non-admin) or any user (admin).
- `GET /api/users/{id}/profile` – Full profile including skills, experiences, educations, job-role preferences.
- `PUT /api/users/{id}` – Update profile. Non-admins cannot change `Email`, `Role`, or `PasswordHash`. `IsOpenToWork` is user-settable.
- `GET /api/users/network` – Public listing for people discovery (no sensitive fields).
- `GET /api/users/me/companies` – Employer's company memberships.
- `PUT /api/users/{id}/open-to-work` – Toggle the `IsOpenToWork` flag for own profile (or admin for any).
- `GET /api/users/lookups/nationalities|jobroles|countries|locations` – Lookup lists for dropdown population.
- `GET /api/users/stats` – Platform-wide stats (user count, company count, opportunity count, active connections).
- `GET /api/users/admin/revenue` – Admin-only: revenue analytics with subscription breakdown and monthly activity.

**`IsOpenToWork` logic:**
- Default: `true` for new users.
- Automatically set to `false` when:
  - A user's employer role request is **approved** (`POST /api/users/{id}/approve-employer`).
  - An applicant **accepts a final job offer** (`POST /api/jobapplications/{id}/applicant-respond` with `Response=1` when `Status=3`).
- Users can toggle it manually via `PUT /api/users/{id}/open-to-work` or via the Edit Profile page.

### Employer Workflow

```
User submits request → Role=3 (Pending)
Admin/CompanyAdmin approves → Role=2 (Employer) + IsOpenToWork=false
Admin/CompanyAdmin rejects → Role=1 (regular user)
Employer resigns voluntarily → Role=1 + IsOpenToWork=true + CompanyMemberships removed
```

- `POST /api/users/me/request-employer` – Upload supporting document (PDF/image ≤5MB) and optional note.
- `POST /api/users/{id}/approve-employer` – Admin or CompanyAdmin approval. Optionally sets `companyMemberRole`.
- `POST /api/users/{id}/reject-employer` – Removes pending status, clears document URL.
- `GET /api/users/pending-employers` – Lists pending users. Employers only see pending users in their own companies.
- `POST /api/users/me/resign-employer` – User removes their own employer status voluntarily. Also removes all company memberships.
- `GET /api/users/company/{companyId}/employers` – Active employers in a company (Role ≥1 in CompanyMembers).
- `GET /api/users/company/{companyId}/employer-requests` – Pending employer requests for a specific company (CompanyAdmin only).
- `DELETE /api/users/company/{companyId}/employers/{memberId}` – CompanyAdmin removes an employer from their company. Also resets the user's Role to 1.

### Opportunities & Matching

**Match algorithm (in `MatchScoreHelper`):**

```
WeightedScore = 0.70 × RoleScore + 0.30 × LocationScore   (when roles are specified)
WeightedScore = LocationScore                               (when no roles specified)
```

- **RoleScore**: Jaccard-like overlap between user's `UserJobPreferences` and opportunity's `RequiredJobRoleIds`.
- **LocationScore**: Exact `LocationId` match → 100; same region → 60; same country → 30; different → 0. Falls back to legacy string comparison.

**Notification on publish:**
- When a new opportunity is created, `NotifyPerfectMatchUsersAsync` is called.
- All job-seeker users (Role=1) are scored against the new opportunity.
- Users with a match score ≥ 90% receive a `Notification` of type `"HighMatchOpportunity"` with the opportunity ID, title, and match score in the `Payload` JSON.

**Key endpoints (`/api/opportunities`):**
- `GET /api/opportunities/recommended` – Personalized for the current user.
- `GET /api/opportunities/with-match` – All opportunities with the user's match score.
- `GET /api/opportunities/{id}/match` – Single opportunity match for the current user.
- `POST /api/opportunities` – Create (Admin/Employer). Triggers match notifications.
- `PUT /api/opportunities/{id}` – Update (Admin or active company member with role≥1).
- `DELETE /api/opportunities/{id}` – Delete. Admin can delete any opportunity. Employers (role=2) can delete opportunities that belong to a company they are an active member of (CompanyMember.Role≥1). Employers cannot delete opportunities with no associated company.
- `GET /api/opportunities/{id}/employer-matches` – Candidate match scores for an employer's opportunity.

### Job Applications Pipeline

**Stages (`JobApplication.Status`):**

| Value | Name | Who sets it |
|---|---|---|
| `0` | Submitted | Applicant (apply endpoint) |
| `1` | Screening | Employer (`screen` action) |
| `2` | Interview | Employer (`interview` action) |
| `3` | Offer | Employer (`offer` action) — Recruiter role cannot do this |
| `4` | Hired | Employer (`hire` action) OR applicant accepts offer (`applicant-respond`, Response=1) |
| `5` | Rejected | Employer (`reject`) OR applicant declines |

- `POST /api/jobapplications/apply` – Enforce Free plan limit (5 applications/month).
- `POST /api/jobapplications/{id}/applicant-respond` – Applicant accepts (1) or declines (2) an interview/offer.
  - Accepting an offer (Status=3 → Status=4) also sets `applicant.IsOpenToWork = false`.
- `POST /api/jobapplications/{id}/employer-action` – Employer moves the pipeline forward or backward.
  - Company Recruiter (Role=1) **cannot** make offers or hire.
- All stage transitions create an `AuditLog` record and send a `Notification` to the affected party.

### Chat & Real-time Presence

**`ChatHub` (SignalR):**

- Users are added to a personal group `user_{userId}` on connect.
- When connecting, the hub broadcasts `UserOnline` events to all chats the user participates in.
- When disconnecting (last connection), the hub broadcasts `UserOffline` events.
- `JoinChat(chatId)` – Verifies the user is a `ChatUser` participant, adds them to the `chat_{chatId}` group, and sends `ParticipantPresence` with current online statuses of all other participants.
- `SendMessage(chatId, text)` – Persists the message and broadcasts `ReceiveMessage` to the group.
- `TypingIndicator(chatId, isTyping)` – Broadcasts `UserTyping` to others in the group.
- `MarkMessageAsRead(chatId, messageId)` – Sets `ReadAt` and notifies the sender.
- `ChatHub.IsUserOnline(userId)` – Static helper for checking online state server-side.
- Online presence state is stored in a `ConcurrentDictionary<int, HashSet<string>>` (userId → connection IDs), which supports multiple simultaneous connections per user.

### Skills & Validation

**Adding a known skill:**
- `POST /api/userskills` – Adds an existing skill to the user's profile immediately. Requires `skillId`.

**Requesting validation for a new skill:**
- `POST /api/userskills/validation-request` (multipart/form-data) – User submits a skill name (new or existing) with an optional supporting document (PDF or image, ≤5MB) and notes.
  - Creates a `SkillValidationRequest` record with `Status=0` (Pending).
  - Notifies all admins (`SkillValidationRequest` notification type).
- `GET /api/userskills/validation-requests/my` – User sees their own request history and statuses.
- `GET /api/userskills/validation-requests` (Admin only) – Lists all requests, optionally filtered by status.
- `POST /api/userskills/validation-requests/{requestId}/review` (Admin only) – Approve or reject.
  - Approval (`Approve=true`): Creates the skill if it doesn't exist, then adds it to the user's `UserSkills`. Sends `SkillValidationApproved` notification.
  - Rejection: Sets `Status=2`. Sends `SkillValidationRejected` notification.

**SkillValidationRequest.Status values:** `0` = Pending, `1` = Approved, `2` = Rejected.

**Skill endorsements:**
- `POST /api/userskills/{userSkillId}/endorse` – Increments `EndorsementCount` and adds a `SkillEndorsement` record. Users cannot endorse their own skills.

### Notifications

- `GET /api/notifications/my` – Returns the last 100 unread + recent notifications for the current user.
- `POST /api/notifications/{id}/markread` – Marks a notification as read.

**Notification types used in the system:**

| Type | Trigger |
|---|---|
| `JobApplied` | Someone applies to an opportunity owned by the user |
| `ApplicationStageChanged` | Employer moves an application to a new stage |
| `ApplicantAccepted` / `ApplicantDeclined` | Applicant responds to an offer/interview |
| `HighMatchOpportunity` | New opportunity published with ≥90% match for the user |
| `EmployerRequest` | A user submits an employer role request (sent to admins) |
| `SkillEndorsed` | Someone endorses the user's skill |
| `SkillValidationRequest` | A skill validation request submitted (sent to admins) |
| `SkillValidationApproved` / `SkillValidationRejected` | Admin reviews a validation request |

### Company Management

- `GET /api/companies` – All companies (session required).
- `POST /api/companies` – Create company (Admin or Employer).
- `PUT /api/companies/{id}` – Update company details.
- `DELETE /api/companies/{id}` – Admin only.
- `POST /api/companies/{id}/upload-logo` – Upload company logo image.
- `GET /api/companymembers` – All members.
- `POST /api/companymembers` – Add member (Admin or Employer, only to own companies for Employer).
- `DELETE /api/companymembers/{id}` – Remove member.
- `PUT /api/users/{id}/company-role` – CompanyAdmin changes another member's role within a company.

### Ratings

- `GET /api/ratings?entityType=User&entityId={id}` – Get ratings for a user or company.
- `POST /api/ratings` – Upsert a rating (1–5 stars + optional review text). Users and companies can be rated.
- Ratings have a `RatedEntityId` + `EntityType` ("User" or "Company") and `Score` (1–5).

### Subscriptions

| Plan | Value | Features |
|---|---|---|
| Free | `0` | 5 job applications/month |
| Pro | `1` | Unlimited applications |
| Enterprise | `2` | Unlimited applications + priority |

- `PUT /api/users/{id}/subscription` – Update a user's subscription plan.
- Admin can view revenue analytics via `GET /api/users/admin/revenue` (monthly/yearly revenue, tax at 23% VAT, profit).

---

## MVC Project (XcelerateLinks)

The MVC app is a thin proxy layer over the API. All data operations are done via HTTP calls to the API using `IHttpClientFactory`.

### Controllers

| Controller | Purpose |
|---|---|
| `AccountController` | Login, register, logout – reads/writes JWT to a cookie |
| `UsersController` | Profile view/edit, employer request flow, resign employer |
| `CompaniesController` | Company listing, details, create/edit, logo upload, employer management panel |
| `OpportunitiesController` | Browse, apply, manage opportunities |
| `ApplicationsController` | Application status for job seekers |
| `ChatsController` | Chat listing, create chat, real-time messaging page |
| `ConnectionsController` | User network connections |
| `HomeController` | Dashboard / landing |
| `SubscriptionsController` | Upgrade/downgrade subscription UI |
| `EmployerContactsController` | Employer candidate contact history |

### Session Management

- The JWT is stored in a cookie named `ApiAccessToken` (HttpOnly, Secure).
- `BaseController.ValidateSessionAsync()` checks the cookie is present and the session is still valid (via `GET /api/auth/validate-session`).
- `BaseController.CreateAuthorizedClient()` creates an `HttpClient` with the `Authorization: Bearer <token>` header set automatically.

### Profile Edit Page (`/Users/Edit`)

The edit page allows:
1. Update basic information (name, email, phone, date of birth).
2. Update professional details (nationality, country, location, job preferences).
3. Update "About Me" bio.
4. Toggle **IsOpenToWork** (switch control).
5. Upload profile picture (via direct API call with JWT from cookie).
6. Upload banner image.
7. **Manage skills** – add existing skills from a searchable dropdown; delete existing skills.
8. **Request new skill** – submit a new skill name + supporting document for admin validation.
9. **Manage experiences** – view, add, delete professional experience entries.

### Company Edit Page (`/Companies/Edit`)

For Admin and Employer users, the edit page also shows:
- **Active Employers** panel – lists all active company members with their roles. CompanyAdmins can remove any member.
- **Pending Employer Requests** panel – lists users who requested to join the company. CompanyAdmins can approve or reject.

### User Profile Page (`/Users/Details`)

- **"Open to Work" badge**: dynamically shows `🟢 Disponível para trabalhar` (green), `💼 A trabalhar` (grey, employed but not open), or `🏢 Empregador` (blue).
- **Resign Employer button**: Shown to the profile owner when they have an employer role. Requires confirmation.
- Displays: About, Experience timeline, Education timeline, Skills list, Job Role Preferences, Ratings.

---

## Database Migrations

Migrations are in `APIPSI16/Migrations/`. EF Core auto-runs pending migrations on startup (in `Program.cs`).

| Migration | Purpose |
|---|---|
| `20260222143117_AddSessionsTable` | Adds `Sessions` table for token tracking |
| `20260301170000_AddRequiredJobRoleIdsToOpportunity` | Adds `RequiredJobRoleIds` (comma-separated) to `Opportunities` |
| `20260302070000_CreateRatingsTable` | Adds `Ratings` table |
| `20260302080000_AddRatingUpdatedAt` | Adds `UpdatedAt` to `Ratings` |
| `20260302090000_AddJobApplicationExtendedFields` | Adds extended fields to `JobApplications` (CoverLetter, PhoneNumber, etc.) |
| `20260302100000_FixJobApplicationSchema` | Schema corrections for `JobApplications` |
| `20260303000001_AddSubscriptionAndEmployerFields` | Adds `SubscriptionPlan`, `EmployerRequestDocumentUrl`, `EmployerRequestNote` to `Users` |
| `20260305000001_AddUsernameLocationAndEmployerMessage` | Adds `Username`, `LocationId`, `CountryId`, `LatestEmployerMessage` |
| `20260307000001_AddIsOpenToWorkAndSkillValidation` | Adds `IsOpenToWork` (bit, default `1`) to `Users`; creates `SkillValidationRequests` table |

---

## Key Services

### `ITokenService` / `TokenService`
- Generates and validates JWT tokens.
- Keys are read from `appsettings.json` (`Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`).
- Supports Base64-encoded keys for environments that require binary-safe config values.

### `ISessionService` / `SessionService`
- Manages `Session` records in the database.
- `CreateSessionAsync(userId, token, expiry)` – Creates a new session record.
- `ValidateSessionAsync(token)` – Checks if a session is active and not expired.
- `InvalidateSessionAsync(token)` – Marks a session as revoked (logout).

### `IFileStorageService` / `FileStorageService`
- `SaveFileAsync(file, folder)` – Saves uploaded file to `wwwroot/uploads/{folder}/` with a GUID filename. Returns a relative URL.
- `DeleteFileAsync(fileUrl)` – Deletes a file from disk.
- `ValidateImageFile(file, out error)` – Validates JPG/PNG/GIF, max 5MB.
- `ValidateDocumentOrImageFile(file, out error)` – Validates JPG/PNG/GIF/PDF, max 5MB. Used for skill validation documents and employer request documents.

### `MatchScoreHelper`
- `ComputeRoleScore(matches, userPrefCount, requiredCount)` – Jaccard-like role overlap score (0–100).
- `ComputeLocationScore(userLocId, oppLocId, userRegion, oppRegion, userCountryCode, oppCountryCode)` – Structured location matching.
- `LocationsMatch(userLocation, oppLocation)` – Legacy string-based location matching fallback.
- `ComputeWeightedScore(roleScore, locationScore, hasRoles)` – Final 70/30 weighted combination.

---

## Notes on Clean Architecture

- The MVC project **never accesses the database directly** – all data flows through the API.
- The API project uses **EF Core Code-First** with explicit migrations.
- **Audit logs** are written for all significant state changes (employer approvals, application stage changes, company member changes).
- **Notifications** are sent best-effort (wrapped in try/catch) so that a notification failure never aborts the primary operation.
- The API uses **role-based authorization** via JWT claims. The MVC layer reads the same claims from the cookie.

---

## Field Validation Rules

### Application Form (`POST /api/jobapplications/apply`)

| Field | Rule |
|---|---|
| `PhoneNumber` | Optional. If provided: 7–20 characters, digits only plus `+`, spaces, `-`, `(`, `)`, `.`. Regex: `^\+?[\d\s\-(). ]{7,20}$` |
| `ProfessionalUrl` | Optional. If provided: must be an absolute URL (validated via `[Url]` attribute). Max 300 characters. |
| `PortfolioUrl` | Optional. If provided: must be an absolute URL. Max 300 characters. |

The `LinkedInUrl` field has been renamed to `ProfessionalUrl` in all C# code and UI labels. The database column retains the name `LinkedInUrl` for backward compatibility; the property is mapped with `[Column("LinkedInUrl")]`.

### Registration (`POST /api/auth/register`)

| Field | Rule |
|---|---|
| `Name` | Required. |
| `Email` | Required. Must be a valid email address format (validated via `[EmailAddress]`). |
| `Password` | Required. Minimum 8 characters. |
| `PhoneNumber` | Optional. If provided: phone format regex (same as above). |

### Profile Edit (`PUT /api/users/{id}`)

| Field | Rule |
|---|---|
| `PhoneNumber` | Optional. If provided: phone format regex validated server-side in `UpdateUser`. |
| `Email` | Admin-only editable. If provided: validated against `^[^@\s]+@[^@\s]+\.[^@\s]+$`. |

### Client-side HTML Validation

All form inputs for phone numbers use `type="tel"` with `pattern="^\+?[\d\s\-(). ]{7,20}$"`.
All URL fields use `type="url"` which browsers validate for absolute URL format.
Email fields use `type="email"` which browsers validate for email format.
Password fields on the registration form use `minlength="8"`.

---

## LinkedIn Removal

All user-facing mentions of "LinkedIn" have been removed from the application UI:

- The footer no longer contains a "LinkedIn" social link.
- The application form field previously labelled "LinkedIn Profile" is now labelled "Professional Profile URL" with a neutral placeholder.
- The translation map in `_Layout.cshtml` was updated accordingly.
- The `JobApplication.LinkedInUrl` property has been renamed to `ProfessionalUrl` in C# code while retaining the `LinkedInUrl` database column name for schema compatibility.
