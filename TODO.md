# TODO

## Refactor to minimize repetition

- [x] Step 1: Identify duplicated patterns across core files (services/viewmodels/windows).
- [x] Step 2: Propose refactor plan (extract helpers, unify repeated logic, reduce inline UI wiring).
- [x] Step 3: Create a small set of helper methods for repeated string/model lookup logic in `MainViewModel`.
- [x] Step 4: Refactor `ModelDownloadService` to remove duplicated model-fetch/sibling parsing logic.
- [x] Step 5: Refactor `ExhibitListWindow` — handler already exists, no duplication found.
- [x] Step 6: Build/tests pass (577/577).

## Future Features

### UCMJ Courts-Martial Venue
Add support for Uniform Code of Military Justice (UCMJ) courts-martial with custom juror demographics:

- **Venue Type**: `JurisdictionType.Military` — distinct from State/Federal
- **Panel Composition**: Courts-martial use a "panel" (not "jury") — typically 5–12 members depending on charge severity. Enlisted accused can request enlisted members (at least 1/3 enlisted).
- **Custom Juror Demographics**:
  - Rank (E-1 through O-10) replaces Income Level as a primary stratification
  - Branch of Service (Army, Navy, Air Force, Marine Corps, Coast Guard, Space Force)
  - Military Occupational Specialty (MOS/rating/AFSC) as analog to Professional Background
  - Years of Service and deployment history
  - Combat arms vs. support vs. administrative roles
  - Security clearance level (affects trust-in-institutions trait)
- **Demographic Database**: `MilitaryDemographicsDatabase` with branch-specific distributions from DMDC (Defense Manpower Data Center) statistics
- **Bias Factor Adjustments**: Military-specific calibrations:
  - Chain-of-command deference → higher RWA baseline
  - Combat veterans → lower punitiveness for violence charges (seen worse)
  - Senior NCOs → higher influence weight (χ) in deliberation (natural leadership)
  - Officers → higher SDO baseline (hierarchical worldview)
  - Junior enlisted → higher conformity (ρ) (accustomed to following orders)
- **Venue-Specific Case Types**:
  - Article 120 (sexual assault) — currently the most common court-martial charge
  - Article 118 (murder)
  - Article 121 (larceny/theft of government property)
  - Article 92 (failure to obey order/regulation)
  - Article 134 (general article — conduct prejudicial to good order and discipline)
- **UI**: Add "Military/UCMJ" option to Case Settings jurisdiction dropdown
- **Jury Instructions**: Military-specific instructions per Manual for Courts-Martial (MCM)
- **Verdict Rules**: 2/3 majority for most charges (not unanimous); unanimous required for death penalty cases