# Role-Based Authorization Plan

## Roles
- SuperAdmin
- Admin
- Worker

## Access Matrix
- `AccountController`
  - `Login`, `AccessDenied`: anonymous
  - `Profile`, `Logout`: authenticated users
- `DashboardController`
  - `Index`: authenticated users (role-specific data)
- `AttendanceController`
  - `Index`, `CheckIn`, `CheckOut`, `History`: Worker/Admin/SuperAdmin as authenticated users
  - `Manage`, `Edit`: Admin and SuperAdmin only
- `AdminController`
  - `Index`: Admin and SuperAdmin only
- `SectionController`
  - `Index`: Admin and SuperAdmin only
- `UserController`
  - `Index`: recommended Admin and SuperAdmin only

## Enforcement Strategy
1. Use `[Authorize]` on all controllers by default.
2. Use `[Authorize(Roles = "SuperAdmin,Admin")]` for admin modules.
3. Keep Worker actions narrow and scoped to self-owned data.
4. Add service-layer checks for section access when needed.

## Claims
- `ClaimTypes.NameIdentifier`: user id
- `ClaimTypes.Role`: one of `SuperAdmin`, `Admin`, `Worker`
- `SectionId`: nullable section identifier

## Notes
- Avoid running parallel EF queries on one DbContext.
- For dashboards and reports, use one grouped attendance query per page where possible.
