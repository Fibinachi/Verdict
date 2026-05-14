# TODO

## Refactor to minimize repetition

- [x] Step 1: Identify duplicated patterns across core files (services/viewmodels/windows).
- [x] Step 2: Propose refactor plan (extract helpers, unify repeated logic, reduce inline UI wiring).
- [x] Step 3: Create a small set of helper methods for repeated string/model lookup logic in `MainViewModel`.
- [x] Step 4: Refactor `ModelDownloadService` to remove duplicated model-fetch/sibling parsing logic.



- [ ] Step 5: Refactor `ExhibitListWindow` to reduce UI handler duplication if any.
- [ ] Step 6: Run build/tests to ensure functionality preserved.

