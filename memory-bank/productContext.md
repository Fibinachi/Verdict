# Product Context

## Why This Project Exists
Verdict simulates courtroom jury trials using AI agents to predict verdict outcomes. It provides legal professionals with a tool to model how different jurors might react to evidence, testimony, and arguments.

## Problems It Solves
1. **Verdict Prediction**: Models how juries might decide based on evidence strength and juror demographics
2. **Case Strategy**: Helps attorneys understand which arguments resonate with different juror profiles
3. **Settlement Analysis**: Estimates settlement values and insurance reserves based on evidence exposure
4. **Jury Selection**: Generates demographically-accurate jury pools for voir dire preparation
5. **Legal Research**: Integrates legal citations (IPC, US Federal Law) for agent context
6. **Document Analysis**: LLM-powered entity extraction from transcripts to auto-populate case files
7. **Role Planning**: Dynamic role assignment recommends optimal courtroom team composition

## How It Should Work
1. User creates/loads a case with charges, evidence, and legal framework
2. Courtroom is populated with AI agents (jurors, judge, lawyers, witnesses)
3. Transcript lines are processed, influencing agent opinions and sentiment
4. Evidence is admitted, shifting verdict leans
5. The simulation progresses through debate stages (opening → testimony → cross → closing → verdict)
6. Jury deliberation produces a predicted verdict
7. Reports can be generated as PDF documents

## User Experience Goals
- Intuitive courtroom visualization with clear seating layout
- Real-time feedback on agent opinions and sentiment
- Easy evidence management with automatic strength assessment
- Seamless save/load of case files
- Professional PDF reports for client presentations
- Multi-provider LLM support for flexible AI agent configuration
- Context menu interactions for agent management (edit, generate, save, delete)
- Drag-and-drop evidence submission via Clerk seat

## Key User Flows

### Case Setup Flow
1. Start new case or load existing .jur file
2. Configure case settings (mode, jurisdiction, charges, evidence)
3. Configure LLM models for AI agent interactions
4. Generate jury panel based on jurisdiction demographics

### Trial Simulation Flow
1. Import or type transcript lines
2. Evidence admission through file dialog or drag-drop
3. Advance through debate stages
4. Monitor jury leanings and sentiment in real-time
5. Generate PDF report at any point

### Agent Management Flow
1. Click "+" on any empty seat to add/activate an agent
2. Right-click occupied seat for context menu (Edit, Generate Juror, Save as .VCS, Delete)
3. Generate Likely Juror fills juror seats with demographically-appropriate profiles
4. Dynamic Role Assignment analyzes case and recommends optimal team composition
