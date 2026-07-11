# Delta for Execution Routing Compatibility

> **Non-authoritative execution-routing metadata.** This contract exists only to make OpenSpec dispatch compatible with the canonical feature. It does not define, amend, duplicate, or override product behavior.

## ADDED Requirements

### Requirement: Canonical authority and drift gate

The executor MUST treat `specs/001-school-enrollment-management/spec.md` as the sole product-requirement authority, MUST preserve by reference all 63 canonical requirement IDs (`REQ-001`–`REQ-063`) and all 35 canonical scenarios (`SCN-001`–`SCN-035`), and MUST block apply when any OpenSpec compatibility artifact drifts from those canonical identifiers or semantics.

#### Scenario: Drift blocks apply

- **GIVEN** an OpenSpec compatibility artifact omits, renumbers, duplicates, reinterprets, or conflicts with a canonical requirement or scenario
- **WHEN** the executor performs its pre-apply authority and drift check
- **THEN** apply is blocked and the canonical root specification remains unchanged
