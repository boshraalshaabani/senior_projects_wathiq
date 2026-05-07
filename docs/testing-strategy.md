# Wathiq Risk-Based Testing Strategy

## Purpose

Wathiq manages protected documents, OCR processing, search indexing, and multi-step review workflows. The testing strategy must therefore focus on the flows that can expose data, block document movement, or delay search availability for end users.

This document defines a company-style quality plan for the `Testing` branch: clear risk priorities, quality gates, measurable outcomes, and phased automation.

## Quality Objectives

1. Prevent cross-institution or cross-department data exposure.
2. Guarantee valid document workflow transitions from draft to archive.
3. Measure OCR turnaround from upload acceptance to searchable document state.
4. Keep search and indexing behavior consistent after workflow or metadata changes.
5. Block secret leakage and broken builds before merge.

## Risk Register

| Risk ID | Area | Why it matters | Likelihood | Impact | Primary automation |
| --- | --- | --- | --- | --- | --- |
| R1 | Authorization | A role bug could expose confidential documents to the wrong institution or department. | Medium | Critical | Unit + integration |
| R2 | Workflow rules | Invalid transitions can freeze review, approval, rejection, or publishing. | High | High | Unit + integration |
| R3 | OCR callback path | OCR failures or delays prevent documents from becoming searchable. | Medium | High | Integration + performance |
| R4 | Search and indexing | Incorrect indexing hides documents or returns unauthorized results. | Medium | High | Unit + performance |
| R5 | Secrets and configuration | Leaked keys or bad environment setup can block delivery and expose services. | Medium | Critical | CI security checks |

## Quality Gates For The Testing Branch

Every push or pull request into `Testing` should pass these gates:

1. Secret scan passes with no leaked credentials.
2. Solution restore and build succeed in a clean environment.
3. High-risk unit tests pass for authorization, workflow, text normalization, and OCR normalization.
4. Test and coverage artifacts are published for review.
5. Publish artifacts for both services can be generated as a staging-style proof.

## Test Architecture

### Unit Tests

Use fast tests to protect business rules and normalization behavior.

Phase 1 targets:

- `DocumentAuthorizationService`
- `DocumentWorkflowService`
- `TextPreprocessorService`
- `NumericValidationService`

Expected value:

- catches logic regressions before they reach API level
- gives fast feedback in CI
- protects the most failure-prone code paths first

### Integration Tests

Use API-level tests to validate real application behavior, not isolated methods.

Phase 2 targets:

- login and protected endpoint access
- OCR callback persistence
- workflow controller endpoints
- search visibility by role

Expected value:

- validates wiring between controllers, services, repositories, and authorization
- proves that the documented business flow works end-to-end within the API

### End-To-End Tests

Use a very small number of realistic scenarios.

Phase 3 target flow:

1. login
2. upload document
3. complete OCR callback
4. submit for review
5. approve or publish
6. search and view the document

### Performance Checks

Use smoke-style measurements on the flows that users feel directly.

Primary KPIs:

- `T1`: upload request accepted
- `T2`: OCR request dispatched
- `T3`: OCR result callback persisted
- `T4`: processed document becomes searchable
- search latency under light concurrent load

This split is more useful than measuring a single OCR wall-clock time because it shows where the bottleneck lives.

## Reporting Model

The branch should produce outputs that look professional and reviewable:

- GitHub Actions status on the repo
- test result artifacts
- coverage artifacts
- staging preview publish artifacts
- a short performance summary with timings for `T1` to `T4`

## Delivery Phases

### Phase 1: Foundation

- CI workflow for secret scan, restore, build, and unit tests
- baseline unit test projects
- risk-focused tests for authorization and workflow

### Phase 2: API Confidence

- integration test project
- protected endpoint checks
- OCR callback and workflow endpoint validation

### Phase 3: User Flow And Runtime Quality

- one realistic E2E scenario
- one performance script with before and after measurements
- short release-readiness summary for the `Testing` branch

## Suggested Team Workflow

- `development`: daily implementation
- `Testing`: quality gate branch
- `main`: approved release branch

Recommended path:

1. implement on `development`
2. open or update a pull request into `Testing`
3. review CI status and artifacts
4. merge to `Testing` only after the quality gates pass

## Current Success Criteria

Phase 1 is considered complete when:

- CI is green on `Testing`
- high-risk unit tests are part of the solution and run automatically
- secret scanning is enforced
- publish artifacts are generated for both backend services
