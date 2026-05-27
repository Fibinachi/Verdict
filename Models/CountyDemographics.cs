using System;
using System.Collections.Generic;

namespace Verdict.Models
{
    /// <summary>
    /// Holds census-derived demographic distributions for a specific county or state.
    /// All arrays are (label, weight) tuples where weights represent proportional
    /// representation in the jury-eligible population (age 18+, citizen).
    /// </summary>
    public class CountyDemographics
    {
        /// <summary>County name (e.g., "St. Croix County") or "Statewide" for state-level</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Two-letter state abbreviation (e.g., "WI")</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>Whether this is state-level data (true) or county-level (false)</summary>
        public bool IsStatewide { get; set; }

        // ── Race/Ethnicity ──
        public List<(string Label, double Weight)> RaceDistribution { get; set; } = new();

        // ── Age (jury-eligible: 18+) ──
        public List<(int Min, int Max, double Weight)> AgeRanges { get; set; } = new();

        // ── Gender ──
        public List<(string Label, double Weight)> GenderDistribution { get; set; } = new();

        // ── Education (25+ population, adjusted for jury pool) ──
        public List<(string Label, double Weight)> EducationDistribution { get; set; } = new();

        // ── Income ──
        public List<(string Label, double Weight)> IncomeDistribution { get; set; } = new();

        // ── Politics (derived from county-level presidential vote shares) ──
        public List<(string Label, double Weight)> PoliticalDistribution { get; set; } = new();

        // ── Religion (derived from state/regional Pew data) ──
        public List<(string Label, double Weight)> ReligionDistribution { get; set; } = new();

        // ── ZIP prefix range for this county ──
        public int ZipMin { get; set; } = 29000;
        public int ZipMax { get; set; } = 29999;

        /// <summary>Median household income for the county (USD)</summary>
        public int MedianIncome { get; set; } = 65000;

        /// <summary>Median age of the county population</summary>
        public double MedianAge { get; set; } = 40.0;

        /// <summary>Urban percentage (0.0–1.0) — influences occupation mix</summary>
        public double UrbanPercent { get; set; } = 0.75;

        /// <summary>College-educated percentage (25+, bachelor's or higher)</summary>
        public double CollegeEducatedPercent { get; set; } = 0.30;
    }
}
