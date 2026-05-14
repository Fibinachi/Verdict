# Verdict - Courtroom Simulation

## Project Overview
Verdict is a WPF desktop application (.NET 9) for modeling jury verdict outcomes using AI agents. The application simulates a courtroom environment where AI-powered agents (jurors, judge, lawyers, witnesses) interact and form opinions based on trial events, evidence, and testimony.

## Core Requirements
- **MVVM Architecture**: Strict Model-View-ViewModel pattern with WPF
- **Multi-LLM Provider Support**: OpenAI, Anthropic, Google Gemini, Ollama, HuggingFace, Alibaba, NVIDIA, Intel, DeepSeek
- **Courtroom Simulation**: Dynamic courtroom layout with judge, jury, counsel tables, gallery
- **Agent System**: Configurable AI agents with demographics, biases, sentiment tracking, verdict leanings
- **Evidence Management**: Admit evidence documents that create trial memories and shift opinions
- **Case File System**: Save/load simulations (.jur files) with full state preservation
- **Character Profiles**: Save reusable agent configurations as .vcs files
- **Five-Stage Courtroom Debate**: Structured debate phases (Opening → Witness → Cross → Closing → Verdict)
- **Legal Database Integration**: IPC/law citations with relevance scoring
- **PDF Case Report Generation**: Export professional reports with QuestPDF
- **Enhanced Document Processing**: LLM entity extraction from transcripts
- **Dynamic Role Assignment**: Auto-assign agents to roles based on case analysis
- **Jury Demographics Generation**: Generate realistic jurors based on county/state census data
- **Agent Interaction System**: LLM-powered agent conversations, examinations, and deliberation

## Target Platform
- Windows 11 (WPF)
- .NET 9 SDK
- C# 12+ with file-scoped namespaces
