# XcelerateLinks – Interview / Oral Defence Q&A

Questions are ordered from easiest to hardest. Each answer references
the exact file and controller where the logic lives.

---

## SECTION 1 – ARCHITECTURE & PROJECT STRUCTURE

### Q1. What are the two applications in this solution and what is the role of each?

**A:**
- **APIPSI16** – the Web API (ASP.NET Core). It owns the database, all
  business logic, and exposes RESTful endpoints under `api/…`. Every route
  is protected with JWT (`[Authorize]`).
- **XcelerateLinks** – the MVC (Razor) web front-end. It has no database;
  it only talks to APIPSI16 over HTTP using `IHttpClientFactory`. The user
  logs in via the MVC, which stores a JWT in an `HttpOnly` cookie and reads
  it back on every request to the API.

### Q2. Why does the MVC app use `IHttpClientFactory` instead of creating an `HttpClient` directly?

**A:**  
`new HttpClient()` creates a new socket on every call and does not respect
DNS changes, leading to socket exhaustion under load.
`IHttpClientFactory` manages a pool of `HttpClientHandler` instances,
reusing connections and recycling them after their lifetime, avoiding these
problems. The client named `"Api"` is configured once in `Program.cs` with
the base address of APIPSI16.

### Q3. How does `CreateAuthorizedClient()` attach the JWT to every API call?
(`XcelerateLinks/Controllers/ApiControllerBase.cs`)

**A:**
```csharp
protected HttpClient CreateAuthorizedClient()
{
    var client = _httpFactory.CreateClient("Api");
    if (Request.Cookies.TryGetValue(TokenHandler.CookieName, out var token)
        && !string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
    return client;
}
```
It reads the JWT from the cookie `ApiAccessToken`, then sets the
`Authorization: Bearer <token>` header. Every MVC controller that inherits
`ApiControllerBase` calls this method, so all API calls are automatically
authenticated.

---

## SECTION 2 – AUTHENTICATION & LOGIN

### Q4. Walk me through what happens when a user logs in.
(`APIPSI16/Controllers/AuthController.cs` → `Login`)

**A (step by step):**
1. The MVC `AccountController.Login` POST receives the username+password,
   creates an `HttpClient` named `"Api"` (no auth yet), and POSTs to
   `api/auth/login`.
2. `AuthController.Login` in the API:
   - Looks up the user by email **or** username (case-insensitive):
     ```csharp
     var user = await _db.Users.FirstOrDefaultAsync(u =>
         (u.Email != null && u.Email.ToLower() == normalized) ||
         (u.Username != null && u.Username.ToLower() == normalized));
     ```
   - Calls `_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, req.Password)`.
     This uses ASP.NET Identity's `PasswordHasher`, which compares the
     bcrypt/PBKDF2 hash stored in the DB against the plaintext supplied.
   - Invalidates all previous active sessions for this user.
   - Calls `_tokenService.CreateToken(...)` to generate a signed JWT
     embedding `NameIdentifier` (userId), `Name`, and `Role` claims.
   - Calls `sessionService.CreateSessionAsync(userId, token, expires)` to
     store the token in the `Sessions` table.
   - Returns `{ token, expiresAt }`.
3. Back in the MVC `AccountController`:
   - Parses the JWT with `JwtSecurityTokenHandler` to extract claims.
   - Appends the token as an `HttpOnly` cookie (expires 1 h).
   - Calls `HttpContext.SignInAsync(CookieAuthenticationDefaults, ...)` to
     create a Forms-auth cookie so MVC `[Authorize]` works.

### Q5. What is `PasswordVerificationResult.SuccessRehashNeeded` and why is it handled?
(`AuthController.cs`)

**A:**
ASP.NET Identity supports multiple hashing algorithms. If the user's stored
hash was created with an older algorithm (e.g. V2 vs V3), verification
succeeds but the framework signals that the password should be re-hashed
with the stronger algorithm:
```csharp
if (verify == PasswordVerificationResult.SuccessRehashNeeded)
{
    user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);
    _db.Users.Update(user);
    await _db.SaveChangesAsync();
}
```
Without this, older accounts would keep their weaker hash forever.

### Q6. Why does the login endpoint invalidate previous sessions before creating a new one?
(`AuthController.cs` and `SessionService.cs`)

**A:**
Allowing multiple concurrent sessions could let an attacker reuse a stolen
token indefinitely. On every login, the API:
```csharp
var previousSessions = await _db.Sessions
    .Where(s => s.UserId == user.UserId && s.IsActive)
    .ToListAsync();
foreach (var session in previousSessions)
{
    session.IsActive = false;
    session.InvalidatedAt = DateTime.UtcNow;
}
```
This ensures only one valid token exists at a time (single active session
per user).

### Q7. How does session validation work in the MVC layer?
(`XcelerateLinks/Controllers/BaseController.cs` → `ValidateSessionAsync`)

**A:**
```csharp
bool isValidSession = await _sessionService.IsSessionValidAsync(userId, token);
if (!isValidSession)
{
    Response.Cookies.Delete(CookieName);
    await HttpContext.SignOutAsync(...);
    return false; // caller redirects to Login
}
```
`IsSessionValidAsync` queries the `Sessions` table:
```csharp
var session = await _db.Sessions.FirstOrDefaultAsync(s =>
    s.UserId == userId && s.Token == token && s.IsActive
    && s.ExpiresAt > DateTime.UtcNow);
return session != null;
```
Even if the JWT itself hasn't expired, the session can be force-invalidated
server-side (e.g. admin logs out a user), and this check catches that.

---

## SECTION 3 – USER MANAGEMENT

### Q8. How does `GetUsers` decide what fields to return and who can filter what?
(`APIPSI16/Controllers/UsersController.cs` → `GetUsers`)

**A:**
```csharp
var q = _context.Users.AsQueryable();
if (userRole != "0")                     // non-admin: see only active users
    q = q.Where(u => u.Role != 0);
if (jobPreference.HasValue)
    q = q.Where(u => u.JobPreference == jobPreference.Value);
if (nationality.HasValue)
    q = q.Where(u => u.Nationality == nationality.Value);
```
`AsQueryable()` creates a lazy LINQ expression tree. Each `.Where()` adds a
SQL `WHERE` clause. The query only hits the DB when `.ToListAsync()` is
called. Admins can see all users; regular users cannot see admin accounts
(`Role != 0` filter).

### Q9. What does `.Include()` do in EF Core and where is it used?
(`APIPSI16/Controllers/UsersController.cs` → `GetUserProfile`)

**A:**
`.Include()` tells EF Core to perform a SQL `JOIN` (or a second query,
depending on the strategy) and populate a navigation property. Without it,
related entities are `null`.

Example:
```csharp
var user = await _context.Users
    .Include(u => u.LocationNav)
        .ThenInclude(l => l != null ? l.Country : null)
    .Include(u => u.CountryNav)
    .FirstOrDefaultAsync(u => u.UserId == id);
```
- `.Include(u => u.LocationNav)` JOINs `Locations` on `Users.LocationId`.
- `.ThenInclude(l => l.Country)` JOINs `Countries` on `Locations.CountryId`
  (a second level of navigation).
- Without these, `user.LocationNav` would be `null` and the API could not
  return `LocationName` or `CountryName`.

### Q10. `GetUserProfile` calls `_context.UserSkills` separately from the main user query. Why not just do one big `.Include()`?
(`UsersController.cs` → `GetUserProfile`)

**A:**
```csharp
var skills = await _context.UserSkills
    .Where(us => us.UserId == id)
    .Include(us => us.Skill)
    .Select(us => new SkillDTO
    {
        SkillId = us.SkillId,
        Name = us.Skill.Name,
        EndorsementCount = _context.SkillEndorsements
            .Count(se => se.UserSkillId == us.UserSkillId)
    })
    .ToListAsync();
```
A single deep `.Include()` on `User → UserSkills → Skill → SkillEndorsements`
would cause a "cartesian explosion": one row per
user×skill×endorsement, potentially thousands of rows. Breaking it into
separate queries keeps each query simple and predictable.
`.Select(us => new SkillDTO { … })` projects directly into a DTO so EF
Core only fetches the columns actually needed.

### Q11. How does updating job preferences work?
(`APIPSI16/Controllers/UsersController.cs` → `UpdateJobPreferences`)

**A:**
```csharp
var existing = await _context.UserJobPreferences
    .Where(p => p.UserId == id)
    .ToListAsync();
_context.UserJobPreferences.RemoveRange(existing);

var newPrefs = preferenceIds.Distinct().Select(roleId =>
    new UserJobPreference { UserId = id, JobRoleId = roleId });
await _context.UserJobPreferences.AddRangeAsync(newPrefs);
await _context.SaveChangesAsync();
```
This is a "delete-and-recreate" pattern: load all current rows, remove
them all with `RemoveRange`, then add the new set. It is simpler than
computing a diff (which rows to add vs which to remove), and the transaction
wrapping EF's `SaveChangesAsync` ensures atomicity.

---

## SECTION 4 – DELETE USER (CASCADE LOGIC)

### Q12. Why does deleting a user require manually deleting related records instead of relying on SQL cascade?

**A:**
Nearly all foreign keys in this schema were created with `NO ACTION`
(the SQL Server default when no `ON DELETE` clause is specified). This
means SQL Server refuses to delete a referenced row while child rows
still point to it. EF Core's `DeleteBehavior.ClientSetNull` only helps when
using change tracking (loading entities first), but `ExecuteDeleteAsync`
bypasses the change tracker and runs raw SQL, so it is entirely subject to
database-level FK constraints.

### Q13. What is `ExecuteDeleteAsync` and why is it used here instead of `RemoveRange`?
(`APIPSI16/Controllers/UsersController.cs` → `DeleteUser`)

**A:**
`ExecuteDeleteAsync` (EF Core 7+) issues a single `DELETE … WHERE …` SQL
statement that never loads entities into memory:
```csharp
await _context.Sessions
    .Where(s => s.UserId == id)
    .ExecuteDeleteAsync();
```
`RemoveRange` would require first loading all matching entities with
`.ToListAsync()`, tracking them in memory, then calling `SaveChangesAsync()`.
For potentially hundreds of records (all sessions, all posts, etc.) across
many tables, `ExecuteDeleteAsync` is far more efficient.

### Q14. Why are `InterviewRounds` deleted in two steps?
(`UsersController.cs` → `DeleteUser`)

**A:**
```csharp
// Step 1 – null out interviewer FK for rounds where this user is assigned
await _context.InterviewRounds
    .Where(r => r.InterviewerUserId == id)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.InterviewerUserId, (int?)null));

// Step 2 – delete rounds that belong to this user's applications
var userApplicationIds = _context.JobApplications
    .Where(a => a.UserId == id)
    .Select(a => a.JobApplicationId);
await _context.InterviewRounds
    .Where(r => userApplicationIds.Contains(r.JobApplicationId))
    .ExecuteDeleteAsync();
```
There are two independent FK relationships:
- `InterviewRound.JobApplicationId` (NOT NULL, NO ACTION) – the interview
  belongs to an application; the application belongs to the user. These
  rounds must be deleted before their parent `JobApplications` are deleted.
- `InterviewRound.InterviewerUserId` (nullable, NO ACTION) – a different
  user can be assigned as the interviewer. Those rounds must NOT be deleted
  (they belong to other applicants), but their `InterviewerUserId` must be
  nulled so the user row can be removed.

### Q15. Why is there an `ExecuteUpdateAsync` step before deleting `PostComments`?
(`UsersController.cs` → `DeleteUser`)

**A:**
`PostComment.ParentCommentId` is a self-referencing nullable FK with NO
ACTION. If User A commented on User B's post and User C replied to User A's
comment, deleting User A's comment would fail because User C's reply still
has `ParentCommentId` pointing to the now-deleted row:
```csharp
var userCommentIds = _context.PostComments
    .Where(c => c.UserId == id)
    .Select(c => c.CommentId);

await _context.PostComments
    .Where(c => c.ParentCommentId != null
             && userCommentIds.Contains(c.ParentCommentId.Value))
    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null));
```
`ExecuteUpdateAsync` issues `UPDATE PostComments SET ParentCommentId = NULL WHERE …`
before the DELETE, breaking the cycle. The replies are preserved but
detached from their parent.

### Q16. Why is `Opportunity.CreatorId` nulled out rather than the opportunity being deleted?

**A:**
```csharp
await _context.Opportunities
    .Where(o => o.CreatorId == id)
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatorId, (int?)null));
```
`Opportunity.CreatorId` is nullable: an opportunity can exist without a
creator (the company is still posting the job). Deleting the opportunity
would also delete all `JobApplications` referencing it and all `InterviewRounds`
for those applications — destroying real business data. Nulling out only the
FK preserves the opportunity and all applications while removing the
constraint that would block the user delete.

### Q17. Why must `SkillEndorsements` be deleted before `UserSkills`?

**A:**
`SkillEndorsement.UserSkillId` is a non-nullable FK to `UserSkills` with NO
ACTION. If we deleted `UserSkills` first, any `SkillEndorsement` row referencing
those skill rows would immediately violate the FK:
```csharp
await _context.SkillEndorsements
    .Where(e => e.EndorserUserId == id || userSkillIds.Contains(e.UserSkillId))
    .ExecuteDeleteAsync();

await _context.UserSkills
    .Where(s => s.UserId == id)
    .ExecuteDeleteAsync();
```
The first delete removes both: endorsements **made by** the user (any skill),
AND endorsements **on** the user's skills (from other endorsers). Only then
can the skill rows themselves be removed.

---

## SECTION 5 – MATCH SCORE ALGORITHM

### Q18. How does the opportunity matching score work?
(`APIPSI16/Services/MatchScoreHelper.cs` and `OpportunitiesController.cs` → `GetOpportunitiesWithMatch`)

**A:**
The score is a weighted combination of two sub-scores:

**Role score (70% weight):**  
Uses the F1-score (harmonic mean of precision and recall):
```csharp
public static int ComputeRoleScore(int matchCount, int userPrefCount, int requiredRoleCount)
{
    int total = userPrefCount + requiredRoleCount;
    return total > 0 ? (int)Math.Round(2.0 * matchCount / total * 100) : 0;
}
```
- `matchCount` = roles in both the user's preferences and the opportunity's
  required roles.
- `2 * matchCount / total` is exactly the F1 formula (harmonic mean of
  precision and recall), scaled to 100.
- Using recall alone would give 100% whenever the user's preferences are a
  superset, even if the opportunity is very different. The F1 penalises
  both surplus preferences and missing requirements.

**Location score (30% weight):**
```csharp
if (userLocationId.Value == oppLocationId.Value) return 100; // exact match
if (same country && SmallCountries.Contains(countryCode))
    return sameRegion ? 65 : 35;           // e.g. Portugal, Netherlands
if (same country && not small)
    return sameRegion ? 50 : 0;            // e.g. US state difference
return 0; // different country
```
**Combined:**
```csharp
public static int ComputeWeightedScore(int roleScore, int locationScore, bool hasRoles)
{
    if (hasRoles && hasLocations)
        return (int)Math.Round(roleScore * 0.7 + locationScore * 0.3);
    if (hasRoles) return roleScore;
    if (hasLocations) return locationScore;
    return 0;
}
```

### Q19. What is the `with-match` endpoint and how does it avoid N+1 queries?
(`OpportunitiesController.cs` → `GetOpportunitiesWithMatch`)

**A:**
N+1 means: 1 query to get all opportunities, then 1 extra query **per**
opportunity to get the user's preferences. With 100 opportunities that
would be 101 queries.

To avoid this, all preferences are loaded once:
```csharp
var userPrefIds = await _context.UserJobPreferences
    .Where(p => p.UserId == uid.Value)
    .Select(p => p.JobRoleId)
    .ToListAsync();   // single DB query → int[]
```
Then scoring happens entirely in memory (C#) inside a `.Select()` lambda
on the already-loaded `opportunities` list. Zero additional DB queries are
needed for the role score calculation.

The opportunities themselves are loaded with `.Include(o => o.Company).Include(o => o.LocationNav).ThenInclude(l => l.Country)` so all needed navigation data comes in one combined query.

---

## SECTION 6 – JOB APPLICATIONS & PIPELINE

### Q20. How does `Apply` prevent duplicate applications?
(`APIPSI16/Controllers/JobApplicationsController.cs` → `Apply`)

**A:**
```csharp
if (await _db.JobApplications.AnyAsync(
    a => a.OpportunityId == dto.OpportunityId && a.UserId == uid))
    return Conflict("Already applied");
```
`AnyAsync` translates to `SELECT CASE WHEN EXISTS(…) THEN 1 ELSE 0 END`
in SQL — it never fetches rows, just returns a boolean, which is more
efficient than `CountAsync() > 0`.

### Q21. How does the monthly Free-plan limit work for job applications?
(`JobApplicationsController.cs` → `Apply`)

**A:**
```csharp
var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
var appThisMonth = await _db.JobApplications
    .CountAsync(a => a.UserId == uid.Value && a.AppliedAt >= monthStart);
if (appThisMonth >= 5)
    return StatusCode(429, ...);
```
`monthStart` is the UTC midnight of the first day of the current month.
The query counts how many applications this user has submitted since that
timestamp. If ≥ 5, an HTTP 429 (Too Many Requests) is returned with a
Portuguese message explaining the limit and how to upgrade.

### Q22. What is the job application state machine and how is it enforced?
(`JobApplicationsController.cs` → `EmployerAction`)

**A:**
Status is stored as a `byte` column:  
`0=Submitted → 1=Screening → 2=Interview → 3=Offer → 4=Hired, 5=Rejected`

Transitions are enforced with a `switch` on `dto.Action`:
```csharp
case "screen":
    if (app.Status != 0) return BadRequest("…");
    newStatus = 1; break;
case "interview":
    if (app.Status != 1) return BadRequest("…");
    newStatus = 2; break;
case "offer":
    if (app.Status != 2) return BadRequest("…");
    newStatus = 3; break;
case "hire":
    if (app.Status != 3) return BadRequest("…");
    newStatus = 4; break;
case "reject":
    newStatus = 5; break; // reject allowed from any state
```
This guarantees the pipeline cannot skip stages (e.g. screen → hire directly)
and only "reject" is a wildcard transition. After the status change an
`AuditLog` row is written and a `Notification` is pushed to the applicant.

### Q23. What does `.Include(a => a.Opportunity).ThenInclude(o => o.Company)` do in the `Get` endpoint?
(`JobApplicationsController.cs` → `Get`)

**A:**
```csharp
var app = await _db.JobApplications
    .Include(a => a.Opportunity).ThenInclude(o => o.Company)
    .Include(a => a.User)
    .Include(a => a.InterviewRounds).ThenInclude(r => r.InterviewerUser)
    .FirstOrDefaultAsync(a => a.JobApplicationId == id);
```
- `.Include(a => a.Opportunity)` JOINs `Opportunities` so `app.Opportunity`
  is not null.
- `.ThenInclude(o => o.Company)` goes one level deeper, JOINing `Companies`
  so `app.Opportunity.Company` is not null (needed to display the company
  name on the application details page).
- `.Include(a => a.InterviewRounds).ThenInclude(r => r.InterviewerUser)`
  loads all interview rounds for the application AND the user assigned as
  interviewer on each round.

Without these includes, all navigation properties would be `null` and the
view would show blank company names and empty interview schedules.

---

## SECTION 7 – CONNECTIONS (SOCIAL NETWORK)

### Q24. How does `MyConnectionsWithUsers` avoid loading user data separately for each connection?
(`APIPSI16/Controllers/ConnectionsController.cs`)

**A:**
```csharp
var connections = await _db.Connections
    .Where(c => (c.RequesterUserId == uid || c.AddresseeUserId == uid) && c.Status == 1)
    .Join(_db.Users,
        c => c.RequesterUserId == uid ? c.AddresseeUserId : c.RequesterUserId,
        u => u.UserId,
        (c, u) => new { c.ConnectionId, …, OtherUser = new { u.UserId, u.Name, … } })
    .ToListAsync();
```
`.Join(…)` is an explicit LINQ join that maps each connection to the
**other** user (i.e., if I am the requester, join on the addressee's ID;
if I am the addressee, join on the requester's ID). EF Core translates this
into a single SQL `JOIN` so all connection data AND the other user's
profile info arrive in one round-trip.

### Q25. How is the Free-plan connection limit enforced in `CreateRequest`?
(`ConnectionsController.cs`)

**A:**
```csharp
var connectionCount = await _db.Connections.CountAsync(c =>
    (c.RequesterUserId == requesterId || c.AddresseeUserId == requesterId)
    && c.Status == 1);
if (connectionCount >= 50)
    return StatusCode(429, ...);
```
Only **accepted** connections (`Status == 1`) count toward the limit.
Pending requests do not. This is checked only when `user.SubscriptionPlan == 0`
(Free plan). Pro/Enterprise users have no limit.

---

## SECTION 8 – COMPANIES

### Q26. How does the `Companies.Details` view get the company's open opportunities without a dedicated endpoint?
(`XcelerateLinks/Controllers/CompaniesController.cs` → `Details`)

**A:**
```csharp
var oppsResp = await client.GetAsync("api/opportunities");
if (oppsResp.IsSuccessStatusCode)
{
    var allOpps = await oppsResp.Content.ReadFromJsonAsync<IEnumerable<Opportunity>>();
    ViewBag.CompanyOpportunities = allOpps?.Where(o => o.CompanyId == id).ToList();
}
```
The controller fetches **all** opportunities from the API and then filters
client-side with `.Where(o => o.CompanyId == id)`. This works fine for a
small dataset; for a larger system a dedicated endpoint
`GET api/opportunities?companyId=…` would be more efficient.

---

## SECTION 9 – ROLE-DISPATCHED VIEWS

### Q27. What does "role-dispatched" mean in this MVC, and show a concrete example.

**A:**
Several `Index` actions choose which Razor view to return based on the
logged-in user's role. Example from `UsersController.cs`:
```csharp
if (IsAdmin())
{
    // ... load users with filters
    return View(model);           // → Views/Users/Index.cshtml (admin table)
}
// Regular user → network/people discovery
return View("UserIndex", users); // → Views/Users/UserIndex.cshtml (grid)
```
`IsAdmin()` (from `ApiControllerBase`):
```csharp
protected bool IsAdmin() => User.FindFirst(ClaimTypes.Role)?.Value == "0";
```
Reads the role claim from the cookie-auth identity set at login. `"0"` is
admin, `"1"` is a regular user, `"2"` is an employer.

### Q28. How does `OpportunitiesController.Index` apply client-side filtering to the match-scored results?
(`XcelerateLinks/Controllers/OpportunitiesController.cs`)

**A:**
```csharp
if (!string.IsNullOrWhiteSpace(q))
    opportunitiesWithMatch = opportunitiesWithMatch.Where(o =>
        (o.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
        (o.LocationName ?? o.Location ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
        .ToList();
```
The API already returns **all** match-scored opportunities. The MVC
controller filters the in-memory `List<OpportunityWithMatch>` with LINQ's
`Where` + `Contains` (using `OrdinalIgnoreCase` for case-insensitive text
search). The filtered list is placed in `ViewBag.OpportunitiesWithMatch`
for the view.

---

## SECTION 10 – EF CORE PATTERNS

### Q29. What is the difference between `AsQueryable()` and `ToListAsync()`?

**A:**
- `AsQueryable()` returns an `IQueryable<T>`. No SQL is executed yet. Each
  subsequent `.Where()`, `.Select()`, `.Include()` etc. appends to the query
  expression tree.
- `ToListAsync()` (or `FirstOrDefaultAsync`, `AnyAsync`, etc.) is the
  "terminal" operator: it compiles the expression tree to SQL and sends it
  to the database.

This deferred execution pattern lets you build complex conditional queries:
```csharp
var q = _context.Users.AsQueryable();
if (jobPreference.HasValue) q = q.Where(u => u.JobPreference == jobPreference);
if (nationality.HasValue)   q = q.Where(u => u.Nationality == nationality);
var result = await q.ToListAsync(); // single SQL query with all conditions
```

### Q30. What is `ExecuteUpdateAsync` and how is it different from loading an entity, changing a property, then calling `SaveChangesAsync`?
(`UsersController.cs` → `DeleteUser`)

**A:**
```csharp
await _context.Opportunities
    .Where(o => o.CreatorId == id)
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatorId, (int?)null));
```
This emits:
```sql
UPDATE Opportunities SET CreatorId = NULL WHERE CreatorId = @id
```
without loading any `Opportunity` into memory.

The classic approach:
```csharp
var opps = await _context.Opportunities.Where(o => o.CreatorId == id).ToListAsync();
foreach (var o in opps) o.CreatorId = null;
await _context.SaveChangesAsync();
```
…loads N objects into the change tracker, one `UPDATE` per object. For
bulk operations, `ExecuteUpdateAsync` is dramatically faster and uses far
less memory.

### Q31. Why is `await using var transaction = await _context.Database.BeginTransactionAsync()` used in `DeleteUser`?

**A:**
The delete spans ~15 separate `ExecuteDeleteAsync`/`ExecuteUpdateAsync`
calls across different tables. Wrapping them in a transaction guarantees
atomicity: if any step fails (e.g. a missed FK), `await transaction.RollbackAsync()`
undoes ALL prior deletes and the database stays consistent. Without a
transaction, a partial failure would leave orphaned rows (e.g. sessions
deleted but the user row still present).

```csharp
await using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // ... all delete steps ...
    _context.Users.Remove(user);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch (Exception ex)
{
    await transaction.RollbackAsync();
    _logger.LogError(ex, "Failed to delete user {UserId}.", id);
    return StatusCode(500, "An error occurred while deleting the user. Please try again.");
}
```

---

## SECTION 11 – MVC FORMS & ROUTING

### Q32. How does the admin delete confirmation page submit the form to the right POST action?
(`XcelerateLinks/Views/Users/Delete.cshtml` and `XcelerateLinks/Controllers/UsersController.cs`)

**A:**
The Razor view generates:
```html
<form asp-action="DeleteConfirmed" asp-route-id="@Model.UserId" method="post" class="d-inline">
    @Html.AntiForgeryToken()
    <button type="submit" class="btn btn-danger">Apagar</button>
</form>
```
`asp-action="DeleteConfirmed"` generates `<form action="/Users/DeleteConfirmed" method="post">`.
`asp-route-id="@Model.UserId"` appends the user ID as a route value in the
URL (e.g. `/Users/DeleteConfirmed?id=5`), so ASP.NET MVC can bind it to
the `int id` parameter via query-string model binding — no hidden field
needed.

The controller has two separate actions:
```csharp
[HttpGet]
public async Task<IActionResult> Delete(int id) { ... }   // shows confirmation page

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteConfirmed(int id)  // performs delete
{ ... }
```
Because the method name and action name both match `DeleteConfirmed`, no
`[ActionName]` attribute is required. The GET and POST are differentiated
solely by their HTTP verb.

### Q33. What is `@Html.AntiForgeryToken()` and why is it required?

**A:**
It renders a hidden `<input>` containing a cryptographic token unique to
this user/session. `[ValidateAntiForgeryToken]` on the POST action checks
that the submitted token matches. This prevents Cross-Site Request Forgery
(CSRF) attacks, where a malicious site tricks a logged-in user into
unknowingly submitting a form to this app.

### Q34. What does `[ValidateAntiForgeryToken]` do if the token is missing or wrong?

**A:**
ASP.NET Core rejects the request with HTTP 400 (Bad Request) before the
action method is even entered. The middleware validates the token from the
form body against a server-side anti-forgery cookie.

---

## SECTION 12 – NOTIFICATIONS

### Q35. Where and how are notifications created on key events?

**A:**
Notifications are created by the API after significant actions using direct
`_db.Notifications.AddAsync`:

- **Connection request sent** (`ConnectionsController.CreateRequest`):
  ```csharp
  await _db.Notifications.AddAsync(new Notification {
      UserId = dto.AddresseeId,    // recipient
      ActorUserId = requesterId.Value, // person who sent the request
      Type = "ConnectionRequest",
      Payload = $"{{\"connectionId\":{conn.ConnectionId}}}",
      ...
  });
  ```
- **Connection accepted**: fires a notification back to the requester
  (`Type = "ConnectionAccepted"`).
- **Job application submitted**: notifies the opportunity creator
  (`JobApplicationsController.Apply`, `Type = "JobApplied"`).
- **Application stage changed**: notifies the applicant
  (`JobApplicationsController.EmployerAction`, `Type = "ApplicationStageChanged"`).
- **Perfect match**: when an opportunity is created, `NotifyPerfectMatchUsersAsync`
  scores all `Role==1` users and sends `Type = "PerfectOpportunityMatch"` to
  those scoring ≥ 90%.

---

## SECTION 13 – TRICKIER / HARDER QUESTIONS

### Q36. Why does the `with-match` endpoint load opportunities with `.Include()` but calculate scores in C# rather than SQL?

**A:**
The scoring formula (F1 for roles + geographic proximity for location)
cannot be expressed in a single SQL query without complex UDFs or stored
procedures. It uses conditional logic (`SmallCountries` lookup, region
comparisons, `HashSet` intersections) that EF Core cannot translate to SQL.

Loading the data into memory (`ToListAsync()` + `.Include()`) and then
running the scoring as a C# `.Select()` lambda on the in-memory list is
the correct approach here. The two pre-loaded lists (`opportunities`,
`userPrefIds`) together prevent any N+1 queries; all remaining work is CPU
time, not I/O.

### Q37. Why does `Apply` wrap the notification creation in `try/catch` but NOT wrap the application insert?
(`JobApplicationsController.cs` → `Apply`)

**A:**
```csharp
await _db.JobApplications.AddAsync(app);
await _db.SaveChangesAsync();  // ← the application MUST succeed

try
{
    // … create notification …
    await _db.SaveChangesAsync();
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Best-effort notification failed …");
}
```
Submitting an application is the primary operation and must be reliable.
Sending a notification is a "nice-to-have" side effect. If the
notification fails (e.g. the opportunity has no creator), the application
should still be saved. The outer `SaveChangesAsync` for the application row
has already committed by the time the notification block runs, so if the
notification fails and throws, the application is unaffected.

### Q38. How does the `ForCompany` endpoint authorise both admins and employers?
(`JobApplicationsController.cs` → `ForCompany`)

**A:**
```csharp
var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
if (userRole != "0") // not admin
{
    var memberRole = await _db.CompanyMembers
        .Where(cm => cm.CompanyId == companyId && cm.UserId == actorId.Value)
        .Select(cm => (int?)cm.Role)
        .FirstOrDefaultAsync();
    if (memberRole == null)
        return Forbid();
}
```
Admins (`Role == "0"`) bypass the check entirely. For employers, the query
looks up whether they are a member of that specific company. If they are not
(`memberRole == null`), it returns 403 Forbidden. The `.Select(cm => (int?)cm.Role)`
pattern uses a nullable cast so that "no row found" returns `null` instead
of throwing, which is cleaner than a full `.FirstOrDefault()` on the entity.

### Q39. How does the MVC `ApplicationsController.Pipeline` read company opportunities from an API response without a typed model?
(`XcelerateLinks/Controllers/ApplicationsController.cs` → `Pipeline`)

**A:**
The company profile endpoint returns a nested JSON structure. Rather than
creating a full DTO, the code uses `System.Text.Json.JsonDocument`:
```csharp
using var doc = await JsonDocument.ParseAsync(
    await oppResp.Content.ReadAsStreamAsync());
if (doc.RootElement.TryGetProperty("opportunities", out var oppsEl))
{
    foreach (var oEl in oppsEl.EnumerateArray())
    {
        oEl.TryGetProperty("id", out var idEl);
        oEl.TryGetProperty("title", out var titleEl);
        oppList.Add(new Opportunity {
            Id = idEl.GetInt32(),
            Title = titleEl.GetString()
        });
    }
}
```
`JsonDocument` gives a DOM-based API to navigate arbitrary JSON without
deserialising into a C# type. `TryGetProperty` is used (rather than
`GetProperty`) so a missing JSON key doesn't throw an exception.

### Q40. Explain the full flow when an admin deletes a user who has: comments on other people's posts, interview rounds as an interviewer, and opportunities they created.

**A (full end-to-end):**

1. Admin clicks "Apagar" on the `Delete.cshtml` confirmation page.
2. The browser POSTs to `POST /Users/Delete` with the user's ID as a hidden
   form field and the anti-forgery token.
3. MVC `UsersController.DeleteConfirmed` calls `CreateAuthorizedClient()`,
   which reads the JWT from the cookie and sets `Authorization: Bearer ...`,
   then sends `DELETE api/users/{id}`.
4. API `UsersController.DeleteUser` begins a database transaction:
   - Null out `InterviewRound.InterviewerUserId` for rounds where this user
     is the interviewer (NO ACTION nullable FK).
   - Delete Interview rounds for this user's job applications (they must go
     before their parent `JobApplications`).
   - Delete `JobApplications`.
   - Before deleting `PostComments`, null out `ParentCommentId` for any reply
     that references one of this user's comments (self-referencing NO ACTION FK).
   - Delete `PostComments` (by user + on user's posts).
   - Null out `Opportunity.CreatorId` for opportunities this user created
     (preserve the opportunities).
   - Delete all remaining dependent rows (sessions, notifications, skills, etc.).
   - Call `_context.Users.Remove(user); await _context.SaveChangesAsync()`.
   - Commit the transaction.
5. API returns `204 No Content`.
6. MVC receives 204, sets `TempData["SuccessMessage"]`, and redirects to
   `/Users/Index` (the admin user list).

---

## SECTION 14 – TABLE NAMING IN EF CORE

### Q41. Why did `DeleteUser` throw `Invalid object name 'EmployerCandidateHistories'` even though the actual SQL table is `EmployerCandidateHistory`?
(`APIPSI16/Data/xcleratesystemslinks_SampleDBContext.cs`)

**A:**
When EF Core generates SQL for a `DbSet<T>`, it uses the `DbSet` **property
name** as the table name unless a `ToTable("…")` mapping is explicitly
configured in `OnModelCreating`.

The `DbSet` was declared as:
```csharp
public virtual DbSet<EmployerCandidateHistory> EmployerCandidateHistories { get; set; }
```
EF Core pluralised property name → `EmployerCandidateHistories`.
Actual SQL table name → `EmployerCandidateHistory` (no trailing 's').

Because there was no `modelBuilder.Entity<EmployerCandidateHistory>` block
at all, EF Core never knew the real table name and generated invalid SQL.

The fix is to add a `ToTable()` call in `OnModelCreating`:
```csharp
modelBuilder.Entity<EmployerCandidateHistory>(entity =>
{
    entity.HasKey(e => e.EmployerCandidateHistoryId)
          .HasName("PK__Employer__7ED6A363F8F4F90F");
    entity.ToTable("EmployerCandidateHistory");
});
```
`ToTable("EmployerCandidateHistory")` tells EF Core the real table name so
every generated SQL statement — `SELECT`, `DELETE`, `UPDATE` — uses the
correct name.

### Q42. Why do some `DbSet` property names differ from the SQL table names in this project?

**A:**
C# convention for collections is the plural form (`EmployerCandidateHistories`,
`AuditLogs`), while SQL table names may be singular or follow a different
naming convention chosen when the database was first designed. EF Core's
default is to use the `DbSet` property name, so whenever the two differ you
**must** add `entity.ToTable("ActualTableName")` in `OnModelCreating`.
This is exactly why all other tables in this project have explicit `ToTable`
entries (e.g. `entity.ToTable("AuditLog")`, `entity.ToTable("Chat")`) —
`EmployerCandidateHistory` was simply missing its entry.

