# TODO: Verdict Phase-Shift Integration (Async Download, Status Tracking, Validation)

## 1. Asynchronous Download & Thread Safety
- [ ] Refactor Model Download Service to async/await
  - [ ] Ensure all Hugging Face and ONNX download routines use non-blocking IO streams (e.g., `HttpClient.GetStreamAsync` or Hugging Face CLI async wrappers).
  - [ ] Remove any `.Result` or `.Wait()` calls from the download service layer that could cause thread deadlocks.

- [ ] Decouple UI Navigation from Download Lifecycle
  - [ ] Move downloading operations into a long-lived, singleton-scoped background worker service: `ModelDownloadManager`.
  - [ ] Ensure navigating away from the model configuration page does not cancel/interrupt the active download task.

## 2. Status Tracking & Event-Driven Engine
- [ ] Implement Thread-Safe Model Status State
  - [ ] Add thread-safe state property to the model configuration data model:
    - `ModelStatus` enum: `Configured`, `Downloading`, `Downloaded`, `Testing`, `Ready`, `Failed`.
  - [ ] Expose safe, observable properties (e.g., `DownloadProgress`, `CurrentStatus`) using thread-safe dispatches so WPF can update UI without cross-thread violations.

- [ ] Create Post-Download Automated Hook
  - [ ] Implement an event-driven hook or background completion callback (`OnDownloadComplete`) that triggers immediately after the file stream closes and verifies successfully on disk.

## 3. Automated Validation & Test Suite
- [ ] Build Isolated Sanity-Check Tester
  - [ ] Create `ModelSanityTester` that runs entirely on a background thread.
  - [ ] Upon download completion, auto-spin a micro-inference loop using the newly downloaded ONNX model.

- [ ] Define Automated Test Criteria
  - [ ] File Integrity: successful model file hash check.
  - [ ] Initialization: ONNX runtime can parse and load graph weights into memory without crashing.
  - [ ] Inference Smoke-Test: pass a generic baseline vector/token array through the model and confirm the output shape is valid.
  - [ ] If all checks pass: set `ModelStatus = Ready`.
  - [ ] If any check fails: log the exception and set `ModelStatus = Failed`.

## Structural Implementation Blueprint (Agent Reference)
[UI: User Configures Model]
       │
       ▼ (Fire-and-Forget on UI Thread)
[Background Worker: Initialize Download]
       │
       ▼ (Background Stream)
[Download Completes Successfully]
       │
       ▼ (Auto-Trigger Event)
[Background Thread: Run ONNX Initialization & Smoke Test]
       │
       ├───► Passes  ──► Set Status to 'Ready'
       └───► Fails   ──► Set Status to 'Failed' & Log Crash

