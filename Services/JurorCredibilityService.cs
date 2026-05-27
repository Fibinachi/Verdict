using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Handles per-juror credibility perception and media-type sensitivity calculations.
/// Extracted from JuryCalculationService to keep files focused and manageable.
/// Each juror assesses witness credibility and responds to evidence media types
/// through their own demographic biases, stereotypes, and cognitive tendencies.
/// </summary>
public static class JurorCredibilityService
{
    /// <summary>
    /// Calculates how a specific juror perceives witness credibility, based on
    /// the juror's own biases, demographics, and cognitive tendencies.
    /// Jurors assess credibility using the same biases, stereotypes, and mental
    /// errors as the general public — not objective accuracy.
    /// Returns a value 0.0-1.0 representing the juror's perceived credibility.
    /// </summary>
    public static double CalculatePerJurorCredibility(Agent juror, EvidenceDocument doc)
    {
        // Start with the document's base witness credibility
        double perceived = doc.WitnessCredibility;

        // If not testimonial, credibility isn't relevant — return neutral
        if (!doc.IsTestimonial) return 0.5;

        // --- Education: more educated jurors are more skeptical of eyewitness testimony ---
        perceived += juror.EducationLevel.ToLower() switch
        {
            "high school" => 0.05,
            "some college" => 0.02,
            "associate degree" => 0.0,
            "bachelor's degree" => -0.03,
            "master's degree" => -0.05,
            "juris doctor (jd)" => -0.08,
            "doctorate (phd)" => -0.06,
            "md" => -0.04,
            _ => 0.0
        };

        // --- Age: older jurors tend to trust authority figures more ---
        if (juror.Age > 55) perceived += 0.08;  // More trusting of authority
        else if (juror.Age < 25) perceived -= 0.05; // More skeptical

        // --- Political affiliation affects trust in institutions ---
        perceived += juror.PoliticalAffiliation.ToLower() switch
        {
            "democratic" => 0.03,   // Slightly more trusting of expert testimony
            "republican" => -0.02,  // Slightly more skeptical of institutional witnesses
            "independent" => 0.0,
            _ => 0.0
        };

        // --- Race/gender interaction: jurors assess credibility through demographic lens ---
        // Research shows cross-race and cross-gender credibility penalties
        // This is a simplified model of documented juror bias patterns
        if (juror.Race != "Unknown" && !string.IsNullOrEmpty(doc.Summary))
        {
            double inGroupBonus = 0.03;

            // Gender-based credibility: women may be perceived differently based on juror gender
            if (juror.Gender == "Female")
            {
                perceived += 0.02 + inGroupBonus; // Female jurors slightly more attuned to witness demeanor
            }
            else
            {
                perceived += inGroupBonus;
            }
        }

        // --- Current status: attentiveness affects credibility assessment ---
        perceived += juror.CurrentStatus.ToLower() switch
        {
            "attentive" => 0.05,
            "focused" => 0.08,
            "skeptical" => -0.12,
            "bored" => -0.05,
            "distracted" => -0.1,
            "sympathetic" => 0.1,   // Sympathetic jurors believe witnesses more
            "hostile" => -0.1,
            _ => 0.0
        };

        // --- Risk perception: risk-averse jurors are more skeptical ---
        perceived += (juror.RiskPerception - 0.5) * -0.15;

        return Math.Clamp(perceived, 0.05, 0.95);
    }

    /// <summary>
    /// Calculates a media-type sensitivity factor for a juror.
    /// Different jurors are swayed differently by different evidence types.
    /// E.g., older jurors may be more swayed by documents; younger jurors by video.
    /// Returns a multiplier around 1.0 (0.7-1.3 range).
    /// </summary>
    public static double CalculateMediaTypeSensitivity(Agent juror, string evidenceCategory)
    {
        double sensitivity = 1.0;

        // --- Age-based media preferences ---
        // Younger jurors are more influenced by video/visual media
        // Older jurors weigh documents and testimony more heavily
        if (juror.Age < 30)
        {
            sensitivity += evidenceCategory switch
            {
                "Video" => 0.15,
                "Image" => 0.08,
                "Audio" => 0.05,
                "Document" => -0.05,
                _ => 0.0
            };
        }
        else if (juror.Age > 55)
        {
            sensitivity += evidenceCategory switch
            {
                "Video" => -0.05,
                "Image" => -0.03,
                "Document" => 0.1,
                "Testimony" => 0.08,
                _ => 0.0
            };
        }

        // --- Education: more educated jurors are more critical of video evidence ---
        if (juror.EducationLevel.Contains("master", StringComparison.OrdinalIgnoreCase) ||
            juror.EducationLevel.Contains("doctor", StringComparison.OrdinalIgnoreCase) ||
            juror.EducationLevel.Contains("jd", StringComparison.OrdinalIgnoreCase))
        {
            sensitivity += evidenceCategory switch
            {
                "Video" => -0.08,     // More skeptical of emotional video appeals
                "Image" => -0.04,
                "Document" => 0.05,   // Prefer documentary evidence
                "Testimony" => -0.05, // More skeptical of eyewitness testimony
                _ => 0.0
            };
        }

        // --- Media consumption: heavy TV viewers are more video-influenced ---
        if (juror.MediaConsumption.Contains("TV", StringComparison.OrdinalIgnoreCase) ||
            juror.MediaConsumption.Contains("24/7", StringComparison.OrdinalIgnoreCase))
        {
            sensitivity += evidenceCategory switch
            {
                "Video" => 0.1,
                "Image" => 0.05,
                _ => 0.0
            };
        }

        // --- Current status ---
        sensitivity += (juror.CurrentStatus.ToLower(), evidenceCategory) switch
        {
            ("bored", "Document") => -0.1,  // Bored jurors don't read documents carefully
            ("distracted", "Document") => -0.15,
            ("focused", "Document") => 0.08,
            ("focused", "Testimony") => 0.1,
            _ => 0.0
        };

        return Math.Clamp(sensitivity, 0.7, 1.3);
    }
}
