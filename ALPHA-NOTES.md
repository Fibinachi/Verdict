# Verdict v0.1.0-alpha ΓÇö Release Notes

**Status:** Open alpha. This is an early build shared for feedback, not a finished product.

## What Verdict is

A courtroom simulator where twelve AI jurors ΓÇö each with a persistent memory, demographic priors, and a model of how human memory degrades ΓÇö hear a case, weigh evidence, and deliberate to a verdict. Built on .NET 9 (WPF, MVVM) for native Windows.

## What works in this alpha

- Full 12-seat jury simulation with belief-updating juror agents
- Multi-provider LLM support: OpenAI, Anthropic, Gemini, DeepSeek, Grok, Ollama, HuggingFace
- Persistent agent memory with decay modeling (verbatim traces fade, gist endures)
- Transcript processing, evidence admission with exhibit tracking
- Deliberation workflow, sidebar discussions, agent chat
- Case files save to `.jur` format; plain-text case reports
- Bias weights grounded in 50+ peer-reviewed studies (see `docs/JurorBiasResearch.md`)

## Known limitations

- **PDF export is disabled in this build.** The PDF generation library was removed for licensing reasons. The export button is visible but greyed out. Text reports and `.jur` files work normally. PDF support will return in a future release under a compatible license.
- **Bring your own API keys.** Verdict does not ship with model access. You will need your own API key for at least one supported provider. If no key is configured, agent responses will be labeled `[Default response: API key missing or unavailable]`.
- **Pre-empirical-calibration.** The bias weights and memory decay parameters are research-grounded defaults, not yet calibrated against real jury data. See `docs/CalibrationAndValidation.md` for the validation plan.
- Expect rough edges. This is an alpha.

## Running it

1. Download `Verdict-v0.1.0-alpha-win-x64.zip` from the release assets below
2. Extract and run `Verdict.exe` (Windows 10/11, x64)
3. Add your API key under Settings for your preferred provider
4. Load or create a case and run the simulation

No installer. No admin rights needed. Your API keys stay on your machine.

## License

PolyForm Noncommercial 1.0.0 ΓÇö free for noncommercial use. See `LICENSE` for the full text. Commercial licensing available from 14QBD273 LLC.

## Feedback

Open an issue at https://github.com/Fibinachi/Verdict/issues. Bug reports with reproduction steps are the most useful thing you can send.
