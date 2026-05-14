# Implementation Plan: Courtroom UI for Jury Verdict Modeling Simulation

**Feature**: Courtroom UI Layout  
**Status**: Draft  
**Reference Spec**: [./spec.md](./spec.md)

## Proposed Changes

### UI Components

#### 1. Courtroom Container
- Create a main container with a layout suitable for a courtroom.
- Use a coordinate or grid system to manage absolute/relative positioning of elements.

#### 2. Seating System
- **Judge's Bench**: Top center, elevated.
- **Witness Stand**: Right of the Judge.
- **Court Reporter Desk**: Left of the Judge.
- **Jury Box**: Left side, 2 columns, 6 rows (12 seats total).
- **Alternate Seats**: 2 seats below the Jury Box.
- **Gallery**: Right side, 12 seats (e.g., 3x4 or 2x6 grid).

#### 3. Counsel Tables & Podium
- **Plaintiff Table**: Center-right, supports dynamic seat additions.
- **Defendant Table**: Center-left, supports dynamic seat additions.
- **Podium**: Centered between tables, clickable.

#### 4. Interaction Layer
- Implement a central chat box (initially hidden or unfocused).
- Connect Podium click event to focus the chat box.

### Data Model
- `CourtroomState`: Tracks occupants for all seats.
- `CounselTeam`: Tracks lawyers and clients for each party.

## Implementation Steps

### Phase 1: Core Layout (P1)
1. **[UI]** Setup main courtroom canvas.
2. **[UI]** Render Judge's Bench, Witness Stand, and Reporter Desk.
3. **[UI]** Render Jury Box (12 seats) and Alternate seats (2).
4. **[UI]** Render Gallery (12 seats).

### Phase 2: Functional Elements (P2)
1. **[UI]** Implement dynamic Counsel Tables.
2. **[UI]** Implement Podium and Central Chat Box.
3. **[Logic]** Wire Podium click to Chat Box focus.

### Phase 3: Refinement (P3)
1. **[UI]** Add basic styling/icons for seats.
2. **[UI]** Ensure responsive behavior for different window sizes.

## Verification Plan

### Automated Tests
- **UI Tests**: Verify all 41+ seats are present in the DOM.
- **Logic Tests**: Verify Podium click triggers chat focus.

### Manual Verification
1. Launch application and visually confirm layout matches spec.
2. Click Podium and confirm chat box interaction.
3. Add multiple participants to a table and verify layout adjustments.
