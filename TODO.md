# Master TODO: Verdict Project

## 1. Core Simulation & Verdict Logic
- [x] Create TODO tracking file (placeholder)
- [ ] Implement criminal vs civil final verdict (criminal: unanimous convict/acquit; civil: majority)
- [ ] Remove damages/dollar influence from criminal trials (skip valuation/considered damages + prevent “damages” cues moving verdict)
- [ ] Update deliberation final verdict display text

## 2. Phase-Shift Integration
### Data Layer & Schema
- [ ] Extend `JurorModel`: Add `PreTrialLean`, `PostTrialLean`, `FinalLean`, and `TrialMemories` buffer.
- [ ] Create `TrialPhase` enum (`PreTrial`, `Trial`, `Deliberation`) and transition utility.
### State Machine & Pipeline
- [ ] Implement `OnTrialStarted` reset routine (copy leans to `PreTrialLean`, reset metrics, fresh buffer).
- [ ] Refactor LLM prompt builder: Mask discovery text during `Trial` phase; prioritize `TrialMemories`.
- [ ] Calculate Deliberation Entry Matrix: `FinalLean = f(PostTrialLean, ProfileBiasWeights)`.
### UI Updates
- [ ] Refactor Aggregate Master Display: Add layers for `PreTrialLean` baseline vs live `PostTrialLean`.
- [ ] Asynchronous Loop Controls: Implement `CancellationTokenSource` for Pause/Interrogate.
- [ ] Juror Seat Interaction: Click-to-chat targeted modal.

## 3. Asynchronous Model Management
- [ ] Refactor Model Download Service to async/await (non-blocking IO, no `.Result`).
- [ ] Decouple UI: Create singleton `ModelDownloadManager` background worker.
- [ ] Implement Thread-Safe `ModelStatus` (`Configured`, `Downloading`, `Downloaded`, `Testing`, `Ready`, `Failed`).
- [ ] Automated Validation: `ModelSanityTester` for background ONNX initialization and smoke tests.

## 4. Evidence Importance & Trust Reporting
### Data Model & Scoring
- [ ] Add `TrustIndex`, `RelianceIndex`, `Importance`, `TrustPolarity`, and `Reason` to agent state.
- [ ] Create `EvidencePreferenceSnapshot` capturing phase-specific scores.
### LLM Extraction & Constraints
- [ ] Enforce structured JSON output for evidence preferences.
- [ ] **Snap Impression Constraint**: `reason` must be exactly one sentence; snap-impression style; no semicolons.
- [ ] Add post-processing normalization to force the `reason` field to one sentence.
- [ ] Hook extraction into `EvidenceAdmissionService` after reporter summary.
### UI & Reporting
- [ ] Add Evidence Trust/Importance panel with color-coded polarity bars.
- [ ] Add unit test for one-sentence normalization and JSON schema validation.

## 5. Documentation & Housekeeping
- [x] README.md: Fix trailing null characters.
- [ ] README.md: Update Deliberation, Evidence admission, Chat orchestration, and stage notes to match current code.
- [ ] Help Files: Refresh `agents.html` and `getting-started.html` for actual UI behavior.
- [ ] Math Docs: Align `docs/math.md` with `Services/ToyJurorLogicEngine.*.cs` and `VerdictLean`.
- [ ] Build/Fixes: Fix missing `ExhibitListItem_MouseDoubleClick` in `Views/ExhibitListWindow.xaml`.

## 6. Current Phase Restrictions (Temporary)
- [ ] Refactor `MainViewModel.CurrentDebateStage` to support dynamic phase switching.
- [ ] Re-enable transcript influence for non-deliberation phases.
- [ ] Implement active UI handlers for Opening Statements, Witness Testimony, and Closing Arguments.

## 7. Future Agent Management Features
*These are currently placeholders in the UI.*
- [ ] **Agent Repository**: Implement `Load Agent` and `Build Agent` logic for `.vcs` file management.
- [ ] **Prompt Engineering**: Implement `Design Prompts` and `Batch Update Prompts` windows.
- [ ] **Layout Customization**: Implement `Courtroom Layout Settings` for dynamic seating configurations.

## 8. Court Phase Implementation (Logic & LLM)
- [ ] **Opening Statements**: Add logic for agents to form initial impressions based on narrative framing.
- [ ] **Witness Testimony**: Implement real-time credibility scoring during Q&A.
- [ ] **Cross-Examination**: Add "hostility" and "impeachment" sentiment modifiers.
- [ ] **Jury Instructions**: Implement legal standard weighting (e.g., "Beyond a Reasonable Doubt" vs "Preponderance of Evidence").

## 9. Verification & Testing Expansion
- [ ] **Service Layer Tests**: Add comprehensive unit tests for:
    - `EvidenceAnalysisService`: Strength assessment and exposure calculation.
    - `DebateService`: Stage advancement and event broadcasting.
    - `LegalDatabaseService`: Citation relevance and search.
    - `ReportGenerationService`: PDF layout validation.
- [ ] **Model Edge Cases**: Test null handling and boundary conditions for `Agent` and `CaseFile` serialization.
- [ ] **Integration Tests**: End-to-end trial simulation from Discovery to Verdict.

---
*Note: This file is the unified source of truth. Fragmented TODO files have been deprecated.*
