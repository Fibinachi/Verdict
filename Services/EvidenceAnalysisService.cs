using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Properties;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Handles evidence strength assessment, damage estimation, and exposure calculation.
/// </summary>
public interface IEvidenceAnalysisService
{
    void AssessStrength(EvidenceDocument doc, string summary, string fileContent = "");
    void CalculateExposure(CaseFile caseFile);
    string InterpretSummary(string summary, CaseFile caseFile);
    string ReadFileContent(string filePath);
    Task<(string Analysis, double TotalDamages)> GenerateDocumentAnalysisAsync(string fileName, string fileContent, string userSummary, CaseFile caseFile, IAgentInteractionService agentInteraction);
}

public class EvidenceAnalysisService : IEvidenceAnalysisService
{
    public void AssessStrength(EvidenceDocument doc, string summary, string fileContent = "")
    {
        // Combine summary and file content for analysis
        string combined = $"{summary} {fileContent}";
        string lower = combined.ToLower();
        double strength = 0.5;   // Default neutral
        double damages = 50000;  // Default moderate estimate

        // --- Parse actual dollar amounts from the content first ---
        double parsedDamages = ParseDollarAmounts(combined);
        if (parsedDamages > 0)
        {
            damages = parsedDamages;
        }

        // --- Apply legal multipliers (treble, double, etc.) to the damages ---
        double multiplier = ParseLegalMultiplier(combined);
        if (multiplier > 1.0)
        {
            damages *= multiplier;
            strength = Math.Max(strength, 0.9); // Multiplied damages strengthen the claim
        }

        // --- Strengthening keywords ---
        if (lower.Contains("medical") || lower.Contains("injury") || lower.Contains("hospital"))
        {
            strength = Math.Max(strength, 0.7);
            if (parsedDamages <= 0) damages = Math.Max(damages, 100000);
        }
        if (lower.Contains("witness") || lower.Contains("testimony") || lower.Contains("saw"))
        {
            strength = Math.Max(strength, 0.6);
            if (parsedDamages <= 0) damages = Math.Max(damages, 25000);
        }
        if (lower.Contains("expert") || lower.Contains("report") || lower.Contains("analysis"))
        {
            strength = Math.Max(strength, 0.8);
            if (parsedDamages <= 0) damages = Math.Max(damages, 150000);
        }
        if (lower.Contains("photo") || lower.Contains("video") || lower.Contains("recording"))
        {
            strength = Math.Max(strength, 0.9);
            if (parsedDamages <= 0) damages = Math.Max(damages, 75000);
        }
        if (lower.Contains("document") || lower.Contains("contract") || lower.Contains("agreement"))
        {
            strength = Math.Max(strength, 0.85);
            if (parsedDamages <= 0) damages = Math.Max(damages, 50000);
        }
        if (lower.Contains("police") || lower.Contains("arrest") || lower.Contains("criminal"))
        {
            strength = Math.Max(strength, 0.95);
            if (parsedDamages <= 0) damages = Math.Max(damages, 200000);
        }
        if (lower.Contains("minor") || lower.Contains("child") || lower.Contains("injured"))
        {
            strength = Math.Max(strength, 0.9);
            if (parsedDamages <= 0) damages = Math.Max(damages, 300000);
        }
        // --- High-value keywords for large claims ---
        if (lower.Contains("million") || lower.Contains("wrongful death") || lower.Contains("catastrophic"))
        {
            strength = Math.Max(strength, 0.85);
            if (parsedDamages <= 0) damages = Math.Max(damages, 1000000);
        }
        if (lower.Contains("billion"))
        {
            strength = Math.Max(strength, 0.9);
            if (parsedDamages <= 0) damages = Math.Max(damages, 1000000000);
        }

        // --- Weakening keywords ---
        if (lower.Contains("hearsay") || lower.Contains("rumor") || lower.Contains("uncertain"))
            strength = Math.Min(strength, 0.3);
        if (lower.Contains("expired") || lower.Contains("old") || lower.Contains("outdated"))
            strength *= 0.5;

        doc.EvidenceStrength = Math.Clamp(strength, 0.0, 1.0);
        doc.EstimatedDamages = damages;
    }

    /// <summary>
    /// Parses dollar amounts from text content (e.g., "$5,000,000", "5 million", "5M").
    /// Returns the highest dollar amount found, or 0 if none found.
    /// </summary>
    private static double ParseDollarAmounts(string text)
    {
        double highestAmount = 0;

        // Pattern 1: $X,XXX,XXX.XX or $X XXX XXX or $X.XX
        var dollarMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"\$([0-9,]+(?:\.[0-9]+)?)");
        foreach (System.Text.RegularExpressions.Match match in dollarMatches)
        {
            if (double.TryParse(match.Groups[1].Value.Replace(",", ""),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
            {
                if (amount > highestAmount) highestAmount = amount;
            }
        }

        // Pattern 2: "X million dollars" or "X million"
        var millionMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"(\d+(?:\.\d+)?)\s*million");
        foreach (System.Text.RegularExpressions.Match match in millionMatches)
        {
            if (double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
            {
                double dollarAmount = amount * 1_000_000;
                if (dollarAmount > highestAmount) highestAmount = dollarAmount;
            }
        }

        // Pattern 3: "X billion dollars" or "X billion"
        var billionMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"(\d+(?:\.\d+)?)\s*billion");
        foreach (System.Text.RegularExpressions.Match match in billionMatches)
        {
            if (double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
            {
                double dollarAmount = amount * 1_000_000_000;
                if (dollarAmount > highestAmount) highestAmount = dollarAmount;
            }
        }

        // Pattern 4: "Xk" or "XK" (e.g., "500k" = $500,000)
        var kMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"(\d+(?:\.\d+)?)\s*[kK](?!\w)");
        foreach (System.Text.RegularExpressions.Match match in kMatches)
        {
            if (double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
            {
                double dollarAmount = amount * 1_000;
                if (dollarAmount > highestAmount) highestAmount = dollarAmount;
            }
        }

        // Pattern 5: "Xm" or "XM" (e.g., "5m" = $5,000,000)
        var mMatches = System.Text.RegularExpressions.Regex.Matches(
            text, @"(\d+(?:\.\d+)?)\s*[mM](?![a-zA-Z])");
        foreach (System.Text.RegularExpressions.Match match in mMatches)
        {
            if (double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double amount))
            {
                double dollarAmount = amount * 1_000_000;
                if (dollarAmount > highestAmount) highestAmount = dollarAmount;
            }
        }

        return highestAmount;
    }

    /// <summary>
    /// Parses legal damage multipliers from text (e.g., "treble damages" = 3x,
    /// "double damages" = 2x, "statutory trebling" = 3x, "punitive multiplier" = 2x).
    /// Returns the multiplier found, or 1.0 if none found.
    /// </summary>
    private static double ParseLegalMultiplier(string text)
    {
        string lower = text.ToLower();

        // --- Explicit multiplier phrases ---

        // "treble damages", "trebling", "treble" → 3x
        if (lower.Contains("treble") || lower.Contains("trebling"))
            return 3.0;

        // "triple damages" → 3x
        if (lower.Contains("triple damages") || lower.Contains("triple the"))
            return 3.0;

        // "double damages" or "doubled" → 2x
        if (lower.Contains("double damages") || lower.Contains("doubled"))
            return 2.0;

        // "statutory multiplier" → check for specific number after it
        var multiplierMatch = System.Text.RegularExpressions.Regex.Match(
            lower, @"multipl(?:ier|ied|y)\s*(?:of\s*)?(\d+(?:\.\d+)?)\s*x");
        if (multiplierMatch.Success)
        {
            if (double.TryParse(multiplierMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double mult))
            {
                if (mult > 1.0) return mult;
            }
        }

        // "X times damages" pattern (e.g., "3 times damages")
        var timesMatch = System.Text.RegularExpressions.Regex.Match(
            lower, @"(\d+(?:\.\d+)?)\s*times\s*(?:the\s*)?damages");
        if (timesMatch.Success)
        {
            if (double.TryParse(timesMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double mult))
            {
                if (mult > 1.0) return mult;
            }
        }

        // --- Contextual indicators that suggest multipliers ---

        // "punitive damages" often carry a multiplier (typically 2-4x)
        // We use a conservative 2x unless a specific number is given
        if (lower.Contains("punitive damages") || lower.Contains("exemplary damages"))
        {
            // Check if a specific ratio is mentioned like "9:1" or "4:1"
            var ratioMatch = System.Text.RegularExpressions.Regex.Match(
                lower, @"(\d+(?:\.\d+)?)\s*:\s*1");
            if (ratioMatch.Success)
            {
                if (double.TryParse(ratioMatch.Groups[1].Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double ratio))
                {
                    if (ratio > 1.0) return ratio;
                }
            }
            return 2.0; // Default punitive multiplier
        }

        // "RICO" claims often have treble damages
        if (lower.Contains("rico") && (lower.Contains("racketeer") || lower.Contains("18 u.s.c")))
            return 3.0;

        // "antitrust" claims often have treble damages
        if (lower.Contains("antitrust") || lower.Contains("anti-trust"))
            return 3.0;

        // "patent infringement" can have treble damages for willful infringement
        if (lower.Contains("willful") && (lower.Contains("patent") || lower.Contains("copyright") || lower.Contains("trademark")))
            return 3.0;

        // "wage" claims can have double damages under FLSA
        if (lower.Contains("flsa") || lower.Contains("fair labor") || lower.Contains("wage") && lower.Contains("overtime"))
            return 2.0;

        // No multiplier detected
        return 1.0;
    }

    public void CalculateExposure(CaseFile caseFile)
    {
        double totalExposure = 0;

        foreach (var doc in caseFile.Evidence)
            totalExposure += doc.EstimatedDamages * doc.EvidenceStrength;

        double multiplier = caseFile.Mode == CaseMode.Civil ? 0.8 : 0.6;
        caseFile.EstimatedSettlement = totalExposure * multiplier;
        caseFile.InsuranceReserve = totalExposure * 1.25; // 25 % buffer
    }

    public string InterpretSummary(string summary, CaseFile caseFile)
    {
        string modelName = caseFile.AvailableModels.FirstOrDefault()?.FriendlyName ?? "Default Model";
        return $"[INTERPRETED BY {modelName.ToUpper()}] {summary}. " +
               $"This document relates to the current {caseFile.Mode} case " +
               $"in {caseFile.CourtName}.";
    }

    /// <summary>
    /// Reads the content of a text-based file. Supports .txt, .rtf, .csv, .json, .xml.
    /// For .pdf and .docx returns the file name and a note that binary format needs extraction.
    /// </summary>
    public string ReadFileContent(string filePath)
    {
        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Text-based formats we can read directly
        if (extension is ".txt" or ".csv" or ".json" or ".xml" or ".log" or ".md" or ".html" or ".htm")
        {
            // Limit to first 50000 characters for thorough LLM analysis
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            return content.Length > 50000 ? content[..50000] + "\n...[truncated]" : content;
        }

        // RTF - strip basic formatting and read as text
        if (extension == ".rtf")
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            // Simple RTF tag stripping
            content = System.Text.RegularExpressions.Regex.Replace(content, @"\\.", " ");
            content = System.Text.RegularExpressions.Regex.Replace(content, @"\{[^}]*\}", " ");
            return content.Length > 50000 ? content[..50000] + "\n...[truncated]" : content;
        }

        // Image files - return descriptive metadata
        if (extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp")
        {
            long fileSizeKb = new FileInfo(filePath).Length / 1024;
            return $"[Image file: {Path.GetFileName(filePath)} - {fileSizeKb} KB, dimensions not available]";
        }

        // Binary formats - extract text content
        if (extension == ".pdf")
        {
            try
            {
                return ExtractTextFromPdf(filePath);
            }
            catch (Exception ex)
            {
                return $"[Error extracting text from PDF: {ex.Message}. " +
                       $"User-provided summary: {Path.GetFileNameWithoutExtension(filePath)}]";
            }
        }
        if (extension == ".docx")
        {
            return $"[Binary document: {Path.GetFileName(filePath)}. Full text extraction requires a document parser. " +
                   $"User-provided summary: {Path.GetFileNameWithoutExtension(filePath)}]";
        }

        return $"[Unknown file format: {extension}. File: {Path.GetFileName(filePath)}]";
    }

    /// <summary>
    /// Uses the LLM to generate a detailed analysis of a document for agent review,
    /// including calculating damages per defendant and total.
    /// Calls the provider directly with a custom analytical prompt (bypasses the
    /// restrictive Reporter system prompt which limits word count and forbids analysis).
    /// </summary>
    public async Task<(string Analysis, double TotalDamages)> GenerateDocumentAnalysisAsync(
        string fileName, string fileContent, string userSummary,
        CaseFile caseFile, IAgentInteractionService agentInteraction)
    {
        // Find the first available model for the analysis
        var model = caseFile.AvailableModels.FirstOrDefault();
        if (model == null)
        {
            // Fallback to keyword-based analysis if no LLM available
            var fallback = GenerateFallbackAnalysis(fileName, userSummary);
            return (fallback, ParseDollarAmounts(userSummary + " " + fileContent));
        }

        try
        {
            // Get the provider directly so we can use our own system prompt
            var provider = ProviderDiscoveryService.GetProviderByName(model.Provider);
            if (provider == null)
            {
                var fallback = GenerateFallbackAnalysis(fileName, userSummary);
                return (fallback, ParseDollarAmounts(userSummary + " " + fileContent));
            }

            // Custom system prompt for thorough document analysis — no word limits
            string systemPrompt = "You are a senior legal analyst and damages expert. Your role is to thoroughly analyze legal documents and provide a comprehensive, detailed analysis that will be used by AI agents (judge, jurors, lawyers) to understand the evidence. There are NO word limits — be as detailed and thorough as possible. \n\n" +
                "Your analysis must cover:\n" +
                "1. Document type and purpose\n" +
                "2. All parties mentioned and their roles\n" +
                "3. Key facts, allegations, data points, and timelines\n" +
                "4. Relevance to the case and potential impact\n" +
                "5. Specific dollar amounts, dates, names, and locations\n" +
                "6. Strengths and weaknesses of the document\n\n" +
                "Be specific. Include exact quotes, numbers, and names from the document. Do not summarize vaguely.";

            string userPrompt = "Analyze the following legal document in detail. Structure your response with these exact sections:\n\n" +
                "=== DOCUMENT OVERVIEW ===\n" +
                "What type of document is this? Who created it? What is its purpose? When was it created?\n\n" +
                "=== KEY PARTIES & ENTITIES ===\n" +
                "List every person, company, or organization mentioned and their role/relationship to the case.\n\n" +
                "=== KEY FACTS & ALLEGATIONS ===\n" +
                "List each specific fact, allegation, claim, or data point. Include exact numbers, dates, names, and locations. Be thorough.\n\n" +
                "=== RELEVANCE TO CASE ===\n" +
                "How is this document relevant to the legal issues in this case? What does it prove or disprove?\n\n" +
                "=== DAMAGES ANALYSIS ===\n" +
                "List all specific dollar amounts mentioned. Break down by category (medical, lost wages, pain and suffering, property damage, etc.) and by defendant if multiple.\n\n" +
                $"Document content:\n{fileContent}\n\n" +
                $"Submitted by: {userSummary}";

            var response = await provider.GenerateResponseAsync(model, systemPrompt, userPrompt);

            if (!string.IsNullOrEmpty(response))
            {
                string analysis = $"[AI ANALYSIS] {response}";

                // Try to extract the total damages from the LLM response
                double llmDamages = ExtractTotalDamagesFromAnalysisPublic(response);

                // Also parse dollar amounts from the raw content as a fallback
                double contentDamages = ParseDollarAmounts(fileContent + " " + userSummary);

                // Use the higher of the two (LLM is smarter, but content parsing is a safety net)
                double totalDamages = llmDamages > 0 ? llmDamages : contentDamages;

                return (analysis, totalDamages);
            }
        }
        catch
        {
            // Fallback on error
        }

        var fallbackAnalysis = GenerateFallbackAnalysis(fileName, userSummary);
        return (fallbackAnalysis, ParseDollarAmounts(userSummary + " " + fileContent));
    }

    /// <summary>
    /// Extracts the total damages amount from the LLM's analysis text.
    /// Looks for the "=== TOTAL DAMAGES ===" section and parses the number.
    /// </summary>
    public static double ExtractTotalDamagesFromAnalysisPublic(string analysis)
    {
        // Look for the TOTAL DAMAGES section
        var sections = analysis.Split(new[] { "=== TOTAL DAMAGES ===", "TOTAL DAMAGES" }, StringSplitOptions.None);
        if (sections.Length > 1)
        {
            string damagesSection = sections[^1]; // Take the last occurrence
            // Parse any dollar amount in this section
            double parsed = ParseDollarAmounts(damagesSection);
            if (parsed > 0) return parsed;
        }

        // Fallback: look for any large dollar amount in the entire analysis
        // Prioritize amounts that appear after "total" or "sum" keywords
        string lower = analysis.ToLower();
        int totalIdx = lower.LastIndexOf("total");
        if (totalIdx >= 0)
        {
            string afterTotal = analysis[totalIdx..];
            double parsed = ParseDollarAmounts(afterTotal);
            if (parsed > 0) return parsed;
        }

        return 0;
    }

     /// <summary>
     /// Generates a keyword-based analysis when no LLM is available.
     /// </summary>
     private static string GenerateFallbackAnalysis(string fileName, string userSummary)
     {
         string lower = userSummary.ToLower();
         var sb = new StringBuilder();
         sb.AppendLine($"Document: {fileName}");
         sb.AppendLine($"Summary: {userSummary}");
         sb.AppendLine();

         if (lower.Contains("medical") || lower.Contains("injury") || lower.Contains("hospital"))
             sb.AppendLine("Analysis: Medical document - likely shows plaintiff injuries or treatment. Strong evidence for damages.");
         else if (lower.Contains("witness") || lower.Contains("testimony"))
             sb.AppendLine("Analysis: Witness statement - provides eyewitness account of events. Moderate evidentiary value.");
         else if (lower.Contains("expert") || lower.Contains("report"))
             sb.AppendLine("Analysis: Expert report - contains professional analysis. High evidentiary value for technical matters.");
         else if (lower.Contains("photo") || lower.Contains("video") || lower.Contains("recording"))
             sb.AppendLine("Analysis: Visual evidence - provides direct visual record. Very high evidentiary impact.");
         else if (lower.Contains("contract") || lower.Contains("agreement"))
             sb.AppendLine("Analysis: Legal document - defines rights and obligations between parties. Key for contractual claims.");
         else if (lower.Contains("police") || lower.Contains("arrest") || lower.Contains("criminal"))
             sb.AppendLine("Analysis: Law enforcement record - official documentation of incident. High credibility evidence.");
         else
             sb.AppendLine("Analysis: General evidentiary document - relevance depends on content. Standard evidentiary weight.");

         sb.AppendLine($"Estimated evidentiary strength: Moderate");
         return sb.ToString();
     }

     /// <summary>
     /// Extracts text content from a PDF file using iText7.
     /// </summary>
     /// <param name="filePath">Path to the PDF file</param>
     /// <returns>Extracted text content</returns>
     private string ExtractTextFromPdf(string filePath)
     {
         var text = new StringBuilder();

         using (var reader = new PdfReader(filePath))
         {
             using (var pdfDoc = new PdfDocument(reader))
             {
                 int numberOfPages = pdfDoc.GetNumberOfPages();
                 
                 for (int pageNumber = 1; pageNumber <= numberOfPages; pageNumber++)
                 {
                     var page = pdfDoc.GetPage(pageNumber);
                     var locationTextExtractor = new iText.Kernel.Pdf.Canvas.Parser.Listener.LocationTextExtractionStrategy();
                     new iText.Kernel.Pdf.Canvas.Parser.PdfCanvasProcessor(locationTextExtractor).ProcessPageContent(page);
                     
                     var pageText = locationTextExtractor.GetResultantText();
                     if (!string.IsNullOrWhiteSpace(pageText))
                     {
                         text.AppendLine(pageText);
                     }
                     
                     // Limit total text to prevent overwhelming the LLM
                     if (text.Length > 100000) // ~100k characters limit
                     {
                         text.AppendLine("\n...[content truncated due to length]");
                         break;
                     }
                 }
             }
         }

         return text.ToString();
     }
 }
