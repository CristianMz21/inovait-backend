# Proposal: School Enrollment Management

## Why

Recover `school-enrollment-management` in the OpenSpec dispatcher without re-planning. Canonical requirements remain exclusively under `specs/001-school-enrollment-management/`; this non-authoritative metadata may record implementation progress but cannot change product behavior.

Prior clarification and PRD review are complete, so no proposal question round is required.

## What Changes

- Restore dispatcher-compatible execution metadata for the already-planned change.
- Reference canonical requirements, design, and task progress without duplicating product behavior.
- Resume implementation from the verified S03 closure at `fb4309f`; S04/V2-T027 is next.

## Scope

### In Scope
- Mirror the validated P0 scope: `US1` enrollment, `US2` enrollment query, and `US3` multi-school teacher contracting.
- Preserve the approved production model, task IDs, P0-before-P1 gate, `stacked-to-main` delivery, and 400-line human-review gate.
- Continue from local `main` through `fb4309f`: S01–S03 and V2-T001–V2-T026 are complete with immutable evidence.

### Out of Scope
- New requirements, behavior, scope, IDs, or architecture decisions.
- Changes to canonical requirements, task IDs/order, product behavior, OpenAPI, or P1 authorization; progress is mirrored from canonical tasks.
- `US4`–`US7` runtime delivery before the P0 gate passes.

## Capabilities

The capability below exists only to satisfy OpenSpec execution routing; it does not introduce product behavior.

### New Capabilities
- `school-enrollment-management`: non-authoritative routing metadata that delegates all product and technical authority to `specs/001-school-enrollment-management/`.

### Modified Capabilities
- None.

## Approach

Use `specs/001-school-enrollment-management/spec.md` for behavior, `specs/001-school-enrollment-management/plan.md` and `specs/001-school-enrollment-management/data-model.md` for the validated technical approach, and `specs/001-school-enrollment-management/tasks.md` for execution status and IDs. OpenSpec change `school-enrollment-management` exists only as the operational compatibility ledger required by native routing. Task synchronization is one way from the canonical root: apply updates root `tasks.md` first and regenerates the OpenSpec ledger in the same work unit; any drift blocks apply.

## Current Routing

- Status: apply-ready.
- Progress: 26 of 103 canonical tasks complete; 77 pending.
- Next recommended phase: `apply`.
- Blocked reasons: none.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `specs/001-school-enrollment-management/` | Reference only | Canonical requirements, design, model, and tasks |
| `openspec/changes/school-enrollment-management/` | Compatibility metadata | Dispatcher-compatible proposal and state |
| Existing implementation through `fb4309f` | Verified | Immutable S03 baseline for S04/V2-T027 |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Mirror drifts from canonical files | Medium | Fail apply and regenerate the ledger one way from canonical root files |
| P1 starts before P0 evidence | Medium | Preserve the existing P0 gate |
| Slice exceeds 400 human lines | High | Preserve defined fallback slices and gates |

## Rollback Plan

Remove only this compatibility mirror. Future implementation rollback remains the existing per-slice revert-and-regate process.

## Dependencies

- Canonical `specs/001-school-enrollment-management/spec.md`, `specs/001-school-enrollment-management/plan.md`, `specs/001-school-enrollment-management/data-model.md`, and `specs/001-school-enrollment-management/tasks.md`.
- OpenSpec execution identity `school-enrollment-management`.

## Success Criteria

- [x] With proposal, specification, design, and tasks present, OpenSpec dispatches next to `apply` at 26/103 progress without changing canonical behavior.
- [x] All 63 canonical requirement IDs and 35 canonical scenarios remain referenced unchanged rather than duplicated into OpenSpec.
- [x] S01–S03 completion through immutable commit `fb4309f` is preserved as the implementation baseline.
