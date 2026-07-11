# Proposal: School Enrollment Management

## Why

Recover `school-enrollment-management` in the OpenSpec dispatcher without re-planning. Canonical requirements remain exclusively under `specs/001-school-enrollment-management/`; this non-authoritative metadata may record implementation progress but cannot change product behavior.

Prior clarification and PRD review are complete, so no proposal question round is required.

## What Changes

- Restore dispatcher-compatible execution metadata for the already-planned change.
- Reference canonical requirements, design, and task progress without duplicating product behavior.
- Resume implementation from verified S06 model checkpoint `28e25a2`; V2-T039 is next.

## Scope

### In Scope
- Mirror the validated P0 scope: `US1` enrollment, `US2` enrollment query, and `US3` multi-school teacher contracting.
- Preserve the approved production model, task IDs, P0-before-P1 gate, `stacked-to-main` delivery, and 400-line human-review gate.
- Continue from local `main` through `28e25a2`: S01–S05 and V2-T001–V2-T038,V2-T040,V2-T041 are complete with immutable evidence; S06 continues at V2-T039.

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
- Progress: 40 of 103 canonical tasks complete; 63 pending.
- Next recommended phase: `apply`.
- Blocked reasons: none.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `specs/001-school-enrollment-management/` | Reference only | Canonical requirements, design, model, and tasks |
| `openspec/changes/school-enrollment-management/` | Compatibility metadata | Dispatcher-compatible proposal and state |
| Existing implementation through `28e25a2` | Verified | Immutable S06 model checkpoint; V2-T039 is the next canonical task |

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

- [x] With proposal, specification, design, and tasks present, OpenSpec dispatches next to `apply` at 40/103 progress without changing canonical behavior.
- [x] All 63 canonical requirement IDs and 35 canonical scenarios remain referenced unchanged rather than duplicated into OpenSpec.
- [x] S01–S05 and V2-T038,V2-T040,V2-T041 completion through immutable commit `28e25a2` are preserved as the implementation baseline; V2-T039 remains pending.
