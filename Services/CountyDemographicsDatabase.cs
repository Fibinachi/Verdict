using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services
{
    /// <summary>
    /// Static database of county-level and state-level demographic distributions
    /// derived from US Census Bureau data (ACS 5-year estimates, 2020 Census,
    /// Pew Religious Landscape Study, and MIT Election Lab county presidential returns).
    /// 
    /// Data is jury-pool-adjusted: distributions represent citizens 18+ with
    /// weighting toward registered voters where applicable.
    /// </summary>
    public static class CountyDemographicsDatabase
    {
        private static readonly Dictionary<string, CountyDemographics> _counties = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CountyDemographics> _states = new(StringComparer.OrdinalIgnoreCase);
        private static CountyDemographics? _nationalDefaults;
        private static bool _initialized;

        /// <summary>
        /// Looks up demographics for a specific county. Returns null if not found.
        /// </summary>
        public static CountyDemographics? LookupCounty(string countyName, string stateAbbr)
        {
            EnsureInitialized();
            string key = NormalizeCountyKey(countyName, stateAbbr);
            return _counties.TryGetValue(key, out var demo) ? demo : null;
        }

        /// <summary>
        /// Looks up state-level demographics. Returns null if not found.
        /// </summary>
        public static CountyDemographics? LookupState(string stateAbbr)
        {
            EnsureInitialized();
            return _states.TryGetValue(stateAbbr, out var demo) ? demo : null;
        }

        /// <summary>
        /// Returns national default demographics (always available).
        /// </summary>
        public static CountyDemographics GetNationalDefaults()
        {
            EnsureInitialized();
            return _nationalDefaults!;
        }

        /// <summary>
        /// Resolves demographics for a jurisdiction: tries county first, then state,
        /// then national defaults.
        /// </summary>
        public static CountyDemographics Resolve(string countyName, string stateName)
        {
            string stateAbbr = StateNameToAbbreviation(stateName);

            // Try exact county match
            var county = LookupCounty(countyName, stateAbbr);
            if (county != null) return county;

            // Try fuzzy county match (strip "County" suffix variations)
            string cleanedCounty = countyName
                .Replace(" County", "")
                .Replace(" county", "")
                .Trim();
            if (!string.Equals(cleanedCounty, countyName, StringComparison.OrdinalIgnoreCase))
            {
                county = LookupCounty(cleanedCounty, stateAbbr);
                if (county != null) return county;
            }

            // Try adding "County" suffix
            county = LookupCounty(cleanedCounty + " County", stateAbbr);
            if (county != null) return county;

            // Fall back to state-level
            var state = LookupState(stateAbbr);
            if (state != null) return state;

            // Ultimate fallback: national defaults
            return GetNationalDefaults();
        }

        private static string NormalizeCountyKey(string county, string stateAbbr)
        {
            // Normalize: strip "County" suffix, trim, lower
            string normalized = county
                .Replace(" County", "")
                .Replace(" county", "")
                .Trim();
            return $"{normalized}|{stateAbbr.ToUpperInvariant()}";
        }

        /// <summary>
        /// Converts a full state name to its two-letter abbreviation.
        /// </summary>
        public static string StateNameToAbbreviation(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName)) return "SC";
            if (stateName.Length == 2) return stateName.ToUpperInvariant();

            return stateName.Trim().ToLowerInvariant() switch
            {
                "alabama" => "AL", "alaska" => "AK", "arizona" => "AZ", "arkansas" => "AR",
                "california" => "CA", "colorado" => "CO", "connecticut" => "CT",
                "delaware" => "DE", "florida" => "FL", "georgia" => "GA",
                "hawaii" => "HI", "idaho" => "ID", "illinois" => "IL", "indiana" => "IN",
                "iowa" => "IA", "kansas" => "KS", "kentucky" => "KY",
                "louisiana" => "LA", "maine" => "ME", "maryland" => "MD",
                "massachusetts" => "MA", "michigan" => "MI", "minnesota" => "MN",
                "mississippi" => "MS", "missouri" => "MO", "montana" => "MT",
                "nebraska" => "NE", "nevada" => "NV", "new hampshire" => "NH",
                "new jersey" => "NJ", "new mexico" => "NM", "new york" => "NY",
                "north carolina" => "NC", "north dakota" => "ND", "ohio" => "OH",
                "oklahoma" => "OK", "oregon" => "OR", "pennsylvania" => "PA",
                "rhode island" => "RI", "south carolina" => "SC", "south dakota" => "SD",
                "tennessee" => "TN", "texas" => "TX", "utah" => "UT",
                "vermont" => "VT", "virginia" => "VA", "washington" => "WA",
                "west virginia" => "WV", "wisconsin" => "WI", "wyoming" => "WY",
                "district of columbia" => "DC",
                _ => stateName.Trim().ToUpperInvariant().Length == 2
                    ? stateName.Trim().ToUpperInvariant()
                    : "SC" // default fallback
            };
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // ═══════════════════════════════════════════════════════════════
            // NATIONAL DEFAULTS (US Census 2020 ACS 5-year, jury-pool adjusted)
            // ═══════════════════════════════════════════════════════════════
            _nationalDefaults = new CountyDemographics
            {
                Name = "National Average",
                State = "US",
                IsStatewide = true,
                ZipMin = 10000, ZipMax = 99999,
                MedianIncome = 75000, MedianAge = 38.8, UrbanPercent = 0.80, CollegeEducatedPercent = 0.34,
                RaceDistribution = new() { ("White",0.60),("Black",0.13),("Hispanic/Latino",0.18),("Asian",0.06),("Native American",0.01),("Pacific Islander",0.005),("Middle Eastern",0.005),("Multiracial",0.025) },
                AgeRanges = new() { (21,29,0.20),(30,39,0.25),(40,49,0.22),(50,59,0.18),(60,70,0.10),(71,85,0.05) },
                GenderDistribution = new() { ("Male",0.49),("Female",0.50),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.28),("Some College",0.28),("Associate Degree",0.10),("Bachelor's Degree",0.22),("Master's Degree",0.08),("Doctorate",0.02),("Trade School",0.02) },
                IncomeDistribution = new() { ("Lower Class",0.15),("Working Class",0.20),("Middle Class",0.35),("Upper Middle Class",0.20),("Upper Class",0.10) },
                PoliticalDistribution = new() { ("Independent",0.42),("Democratic",0.30),("Republican",0.27),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.45),("Catholic",0.22),("Jewish",0.02),("Muslim",0.01),("Buddhist",0.01),("Hindu",0.01),("Non-religious",0.20),("Other Christian",0.05),("Other Faith",0.03) }
            };

            // ═══════════════════════════════════════════════════════════════
            // STATE-LEVEL DATA
            // ═══════════════════════════════════════════════════════════════

            // ── WISCONSIN ──
            _states["WI"] = new CountyDemographics
            {
                Name = "Wisconsin (Statewide)", State = "WI", IsStatewide = true,
                ZipMin = 53001, ZipMax = 54990, MedianIncome = 67200, MedianAge = 40.0,
                UrbanPercent = 0.70, CollegeEducatedPercent = 0.31,
                RaceDistribution = new() { ("White",0.81),("Black",0.065),("Hispanic/Latino",0.07),("Asian",0.03),("Native American",0.01),("Pacific Islander",0.001),("Middle Eastern",0.002),("Multiracial",0.022) },
                AgeRanges = new() { (21,29,0.18),(30,39,0.23),(40,49,0.21),(50,59,0.20),(60,70,0.12),(71,85,0.06) },
                GenderDistribution = new() { ("Male",0.495),("Female",0.495),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.31),("Some College",0.30),("Associate Degree",0.12),("Bachelor's Degree",0.18),("Master's Degree",0.06),("Doctorate",0.01),("Trade School",0.02) },
                IncomeDistribution = new() { ("Lower Class",0.13),("Working Class",0.22),("Middle Class",0.38),("Upper Middle Class",0.20),("Upper Class",0.07) },
                PoliticalDistribution = new() { ("Independent",0.40),("Democratic",0.29),("Republican",0.30),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.44),("Catholic",0.27),("Jewish",0.01),("Muslim",0.01),("Buddhist",0.01),("Hindu",0.01),("Non-religious",0.20),("Other Christian",0.04),("Other Faith",0.01) }
            };

            // ── St. Croix County, WI (Apple River case) ──
            // ~94% White, median income ~$82K, Trump+10, median age ~39
            AddCounty(new CountyDemographics
            {
                Name = "St. Croix County", State = "WI", IsStatewide = false,
                ZipMin = 54001, ZipMax = 54028, MedianIncome = 82000, MedianAge = 39.2,
                UrbanPercent = 0.60, CollegeEducatedPercent = 0.34,
                RaceDistribution = new() { ("White",0.94),("Black",0.015),("Hispanic/Latino",0.022),("Asian",0.015),("Native American",0.004),("Pacific Islander",0.001),("Middle Eastern",0.001),("Multiracial",0.017) },
                AgeRanges = new() { (21,29,0.15),(30,39,0.25),(40,49,0.23),(50,59,0.20),(60,70,0.12),(71,85,0.05) },
                GenderDistribution = new() { ("Male",0.50),("Female",0.49),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.24),("Some College",0.30),("Associate Degree",0.13),("Bachelor's Degree",0.22),("Master's Degree",0.08),("Doctorate",0.02),("Trade School",0.01) },
                IncomeDistribution = new() { ("Lower Class",0.08),("Working Class",0.20),("Middle Class",0.37),("Upper Middle Class",0.25),("Upper Class",0.10) },
                PoliticalDistribution = new() { ("Independent",0.38),("Democratic",0.26),("Republican",0.35),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.43),("Catholic",0.28),("Jewish",0.01),("Muslim",0.005),("Buddhist",0.01),("Hindu",0.005),("Non-religious",0.21),("Other Christian",0.04),("Other Faith",0.01) }
            });

            // ── SOUTH CAROLINA ──
            _states["SC"] = new CountyDemographics
            {
                Name = "South Carolina (Statewide)", State = "SC", IsStatewide = true,
                ZipMin = 29001, ZipMax = 29945, MedianIncome = 58500, MedianAge = 40.1,
                UrbanPercent = 0.67, CollegeEducatedPercent = 0.29,
                RaceDistribution = new() { ("White",0.63),("Black",0.26),("Hispanic/Latino",0.06),("Asian",0.02),("Native American",0.005),("Pacific Islander",0.001),("Middle Eastern",0.003),("Multiracial",0.021) },
                AgeRanges = new() { (21,29,0.19),(30,39,0.22),(40,49,0.21),(50,59,0.20),(60,70,0.12),(71,85,0.06) },
                GenderDistribution = new() { ("Male",0.485),("Female",0.505),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.30),("Some College",0.30),("Associate Degree",0.10),("Bachelor's Degree",0.18),("Master's Degree",0.07),("Doctorate",0.02),("Trade School",0.03) },
                IncomeDistribution = new() { ("Lower Class",0.17),("Working Class",0.23),("Middle Class",0.35),("Upper Middle Class",0.18),("Upper Class",0.07) },
                PoliticalDistribution = new() { ("Independent",0.38),("Democratic",0.26),("Republican",0.35),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.55),("Catholic",0.10),("Jewish",0.01),("Muslim",0.01),("Buddhist",0.005),("Hindu",0.005),("Non-religious",0.18),("Other Christian",0.10),("Other Faith",0.02) }
            };

            // ── Richland County, SC ──
            AddCounty(new CountyDemographics
            {
                Name = "Richland County", State = "SC", IsStatewide = false,
                ZipMin = 29001, ZipMax = 29290, MedianIncome = 56000, MedianAge = 33.5,
                UrbanPercent = 0.85, CollegeEducatedPercent = 0.39,
                RaceDistribution = new() { ("White",0.44),("Black",0.46),("Hispanic/Latino",0.05),("Asian",0.03),("Native American",0.004),("Pacific Islander",0.001),("Middle Eastern",0.004),("Multiracial",0.021) },
                AgeRanges = new() { (21,29,0.25),(30,39,0.24),(40,49,0.18),(50,59,0.16),(60,70,0.11),(71,85,0.06) },
                GenderDistribution = new() { ("Male",0.48),("Female",0.51),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.22),("Some College",0.25),("Associate Degree",0.08),("Bachelor's Degree",0.24),("Master's Degree",0.12),("Doctorate",0.05),("Trade School",0.04) },
                IncomeDistribution = new() { ("Lower Class",0.18),("Working Class",0.22),("Middle Class",0.32),("Upper Middle Class",0.20),("Upper Class",0.08) },
                PoliticalDistribution = new() { ("Independent",0.35),("Democratic",0.43),("Republican",0.21),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.50),("Catholic",0.08),("Jewish",0.02),("Muslim",0.02),("Buddhist",0.01),("Hindu",0.01),("Non-religious",0.22),("Other Christian",0.10),("Other Faith",0.04) }
            });

            // ── NORTH CAROLINA ──
            _states["NC"] = new CountyDemographics
            {
                Name = "North Carolina (Statewide)", State = "NC", IsStatewide = true,
                ZipMin = 27006, ZipMax = 28909, MedianIncome = 61200, MedianAge = 39.4,
                UrbanPercent = 0.66, CollegeEducatedPercent = 0.32,
                RaceDistribution = new() { ("White",0.63),("Black",0.21),("Hispanic/Latino",0.10),("Asian",0.03),("Native American",0.015),("Pacific Islander",0.001),("Middle Eastern",0.003),("Multiracial",0.021) },
                AgeRanges = new() { (21,29,0.20),(30,39,0.23),(40,49,0.21),(50,59,0.19),(60,70,0.11),(71,85,0.06) },
                GenderDistribution = new() { ("Male",0.488),("Female",0.502),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.26),("Some College",0.29),("Associate Degree",0.11),("Bachelor's Degree",0.21),("Master's Degree",0.08),("Doctorate",0.03),("Trade School",0.02) },
                IncomeDistribution = new() { ("Lower Class",0.15),("Working Class",0.22),("Middle Class",0.36),("Upper Middle Class",0.19),("Upper Class",0.08) },
                PoliticalDistribution = new() { ("Independent",0.40),("Democratic",0.28),("Republican",0.31),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.50),("Catholic",0.12),("Jewish",0.01),("Muslim",0.01),("Buddhist",0.01),("Hindu",0.01),("Non-religious",0.20),("Other Christian",0.10),("Other Faith",0.04) }
            };

            // ── CALIFORNIA ──
            _states["CA"] = new CountyDemographics
            {
                Name = "California (Statewide)", State = "CA", IsStatewide = true,
                ZipMin = 90001, ZipMax = 96162, MedianIncome = 84000, MedianAge = 37.3,
                UrbanPercent = 0.95, CollegeEducatedPercent = 0.35,
                RaceDistribution = new() { ("White",0.36),("Black",0.06),("Hispanic/Latino",0.39),("Asian",0.15),("Native American",0.01),("Pacific Islander",0.005),("Middle Eastern",0.010),("Multiracial",0.030) },
                AgeRanges = new() { (21,29,0.22),(30,39,0.26),(40,49,0.20),(50,59,0.17),(60,70,0.10),(71,85,0.05) },
                GenderDistribution = new() { ("Male",0.497),("Female",0.493),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.22),("Some College",0.26),("Associate Degree",0.08),("Bachelor's Degree",0.24),("Master's Degree",0.10),("Doctorate",0.03),("Trade School",0.07) },
                IncomeDistribution = new() { ("Lower Class",0.13),("Working Class",0.18),("Middle Class",0.34),("Upper Middle Class",0.23),("Upper Class",0.12) },
                PoliticalDistribution = new() { ("Independent",0.35),("Democratic",0.42),("Republican",0.22),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.30),("Catholic",0.33),("Jewish",0.03),("Muslim",0.02),("Buddhist",0.02),("Hindu",0.02),("Non-religious",0.23),("Other Christian",0.03),("Other Faith",0.02) }
            };

            // ── TEXAS ──
            _states["TX"] = new CountyDemographics
            {
                Name = "Texas (Statewide)", State = "TX", IsStatewide = true,
                ZipMin = 75001, ZipMax = 79999, MedianIncome = 67500, MedianAge = 35.6,
                UrbanPercent = 0.84, CollegeEducatedPercent = 0.31,
                RaceDistribution = new() { ("White",0.41),("Black",0.13),("Hispanic/Latino",0.40),("Asian",0.05),("Native American",0.005),("Pacific Islander",0.001),("Middle Eastern",0.004),("Multiracial",0.020) },
                AgeRanges = new() { (21,29,0.23),(30,39,0.25),(40,49,0.20),(50,59,0.17),(60,70,0.10),(71,85,0.05) },
                GenderDistribution = new() { ("Male",0.496),("Female",0.494),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.26),("Some College",0.29),("Associate Degree",0.08),("Bachelor's Degree",0.20),("Master's Degree",0.08),("Doctorate",0.02),("Trade School",0.07) },
                IncomeDistribution = new() { ("Lower Class",0.15),("Working Class",0.22),("Middle Class",0.34),("Upper Middle Class",0.20),("Upper Class",0.09) },
                PoliticalDistribution = new() { ("Independent",0.38),("Democratic",0.27),("Republican",0.34),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.47),("Catholic",0.28),("Jewish",0.01),("Muslim",0.02),("Buddhist",0.01),("Hindu",0.01),("Non-religious",0.16),("Other Christian",0.03),("Other Faith",0.01) }
            };

            // ── NEW YORK ──
            _states["NY"] = new CountyDemographics
            {
                Name = "New York (Statewide)", State = "NY", IsStatewide = true,
                ZipMin = 10001, ZipMax = 14925, MedianIncome = 75100, MedianAge = 39.2,
                UrbanPercent = 0.88, CollegeEducatedPercent = 0.37,
                RaceDistribution = new() { ("White",0.55),("Black",0.15),("Hispanic/Latino",0.19),("Asian",0.09),("Native American",0.005),("Pacific Islander",0.002),("Middle Eastern",0.005),("Multiracial",0.018) },
                AgeRanges = new() { (21,29,0.21),(30,39,0.23),(40,49,0.20),(50,59,0.19),(60,70,0.11),(71,85,0.06) },
                GenderDistribution = new() { ("Male",0.485),("Female",0.505),("Non-binary/Other",0.01) },
                EducationDistribution = new() { ("High School",0.25),("Some College",0.24),("Associate Degree",0.10),("Bachelor's Degree",0.22),("Master's Degree",0.12),("Doctorate",0.03),("Trade School",0.04) },
                IncomeDistribution = new() { ("Lower Class",0.14),("Working Class",0.19),("Middle Class",0.33),("Upper Middle Class",0.22),("Upper Class",0.12) },
                PoliticalDistribution = new() { ("Independent",0.35),("Democratic",0.38),("Republican",0.26),("Other/None",0.01) },
                ReligionDistribution = new() { ("Protestant",0.30),("Catholic",0.35),("Jewish",0.08),("Muslim",0.03),("Buddhist",0.02),("Hindu",0.02),("Non-religious",0.18),("Other Christian",0.02),("Other Faith",0.03) }
            };
        }

        private static void AddCounty(CountyDemographics demo)
        {
            string key = NormalizeCountyKey(demo.Name, demo.State);
            _counties[key] = demo;
        }
    }
}
