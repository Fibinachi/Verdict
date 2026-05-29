using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Records the directionality of a bias factor on verdict outcomes,
/// identifying which pipeline stage the direction applies to.
/// </summary>
public readonly struct BiasDirection
{
    /// <summary>Name of the bias factor (e.g., "Political Affiliation", "Religion").</summary>
    public string FactorName { get; }

    /// <summary>Direction of effect: PROSECUTION, DEFENSE, NEUTRAL, or CONTEXTUAL.</summary>
    public string Direction { get; }

    /// <summary>Pipeline stage where this direction is primarily active: g₁ (static bias), g₂ (evidence drift), or g₃ (deliberation).</summary>
    public string Stage { get; }

    public BiasDirection(string factorName, string direction, string stage)
    {
        FactorName = factorName;
        Direction = direction;
        Stage = stage;
    }

    /// <summary>
    /// Returns the canonical bias direction mappings for all 13 factors.
    /// These correspond to the Directionality column in docs/JurorBiasResearch.md.
    /// </summary>
    public static IReadOnlyList<BiasDirection> CanonicalDirections { get; } = new[]
    {
        new BiasDirection("Political Affiliation", "PROSECUTION/DEFENSE", "g₁"),
        new BiasDirection("Education Level", "NEUTRAL", "g₁,g₂"),
        new BiasDirection("Legal Knowledge", "NEUTRAL", "g₂,g₃"),
        new BiasDirection("Juror Experience", "NEUTRAL", "g₃"),
        new BiasDirection("Community Ties", "CONTEXTUAL", "g₃"),
        new BiasDirection("Ethnicity", "CONTEXTUAL", "g₁,g₂"),
        new BiasDirection("Age", "CONTEXTUAL", "g₁,g₂"),
        new BiasDirection("Professional Background", "NEUTRAL", "g₂,g₃"),
        new BiasDirection("Income Level", "CONTEXTUAL", "g₁,g₂"),
        new BiasDirection("Gender", "CONTEXTUAL", "g₁,g₂"),
        new BiasDirection("Religion", "CONTEXTUAL", "g₁,g₂"),
        new BiasDirection("Communication Style", "NEUTRAL", "g₃"),
        new BiasDirection("Firearm Ownership (NRA)", "DEFENSE", "g₁,g₂,g₃"),
    };
}

/// <summary>
/// Service for generating jury pools based on realistic demographic distributions
/// derived from US Census data patterns for specific counties and regions.
/// Uses CountyDemographicsDatabase for jurisdiction-specific distributions.
/// </summary>
public interface IJuryDemographicsService
{
    /// <summary>
    /// Generates a jury panel with the specified number of jurors and alternates.
    /// Demographics are calibrated to the specified county/state.
    /// </summary>
    /// <param name="jurorCount">Number of regular jurors (typically 12)</param>
    /// <param name="alternateCount">Number of alternate jurors (typically 2)</param>
    /// <param name="county">County name for demographic calibration (e.g., "St. Croix County")</param>
    /// <param name="state">State name or abbreviation (e.g., "Wisconsin" or "WI")</param>
    /// <returns>List of Agent objects representing the jury panel</returns>
    List<Agent> GenerateJuryPanel(int jurorCount, int alternateCount, string county, string state = "South Carolina");

    /// <summary>
    /// Generates a single juror with demographics drawn from county/state population distributions.
    /// </summary>
    Agent GenerateJuror(string county, string state = "South Carolina");
}

/// <summary>
/// Implementation of jury demographics generation using probabilistic models
/// based on jurisdiction-specific census data from CountyDemographicsDatabase.
/// </summary>
public class JuryDemographicsService : IJuryDemographicsService
{
    private static readonly Random _random = new();

    // Resolved demographics for the current generation batch (set per-call)
    private CountyDemographics _demos = CountyDemographicsDatabase.GetNationalDefaults();

    // ═══════════════════════════════════════════════════════════════
    // NAME DATABASES (by ethnicity — names are culturally tied, not geographic)
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    // NAME DATABASES (by ethnicity — names are culturally tied, not geographic)
    // ═══════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, string[]> FirstNameEthnicityMap = new()
    {
        { "White", new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", 
                          "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Christopher", "Karen", 
                          "Charles", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Helen", "Mark", "Sandra", 
                          "Donald", "Donna", "Steven", "Carol", "Paul", "Ruth", "Andrew", "Sharon", "Joshua", "Michelle", 
                          "Kenneth", "Laura", "Kevin", "Emily", "Brian", "Kimberly", "George", "Amy", "Edward", "Angela" } },
        { "Black", new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", 
                         "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Derrick", "Tiffany", 
                         "Marcus", "Shaniqua", "Jerome", "Precious", "Andre", "Tanisha", "Reginald", "Shaneka", "Maurice", "Keisha", 
                         "Lamont", "Yolanda", "Terrell", "Latoya", "Darnell", "Tasha", "Jamal", "Nichole", "Deonte", "Ebony", 
                         "Tyree", "Aaliyah", "Cornelius", "Diamond", "Tyrone", "Candice", "Malik", "Carmen", "Lamar", "Felicia" } },
        { "Hispanic/Latino", new[] { "Carlos", "Maria", "Miguel", "Sofia", "Juan", "Isabella", "Diego", "Valentina", "Luis", "Camila", 
                                   "Javier", "Luciana", "Andres", "Guadalupe", "Manuel", "Penelope", "Felipe", "Elena", "Ricardo", "Victoria", 
                                   "Sergio", "Scarlett", "Rafael", "Alexa", "Arturo", "Claudia", "Enrique", "Juliette", "Alberto", "Ariana", 
                                   "Emilio", "Natalia", "Hector", "Carolina", "Francisco", "Esmeralda", "Roberto", "Antonella", "Cristian", "Mariana", 
                                   "José", "Paulina", "Angel", "Madeleine", "Adrian", "Amanda", "Gerardo", "Valeria", "Eduardo", "Gabriela" } },
        { "Asian", new[] { "David", "Jennifer", "James", "Lisa", "John", "Amy", "Robert", "Julie", "Michael", "Stephanie", 
                         "William", "Michelle", "Richard", "Nicole", "Mark", "Samantha", "Steven", "Rebecca", "Joseph", "Lauren", 
                         "Charles", "Katie", "Thomas", "Rachel", "Christopher", "Heather", "Daniel", "Maria", "Matthew", "Christina", 
                         "Anthony", "Kathryn", "Donald", "Andrea", "Paul", "Yuki", "Kenneth", "Mei", "George", "Lin", 
                         "Edward", "Chen", "Jason", "Wang", "Brian", "Li", "Kevin", "Zhang", "Jeffrey", "Liu" } },
        { "Native American", new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", 
                                  "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Christopher", "Karen", 
                                  "Charles", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Helen", "Mark", "Sandra", 
                                  "Joseph", "Donna", "Austin", "Cheyenne", "Dakota", "Sierra", "Cody", "Aubrey", "Devon", "Bailey", 
                                  "Skyler", "Taylor", "Shiloh", "River", "Logan", "Riley", "Peyton", "Sage", "Quinn", "Avery" } },
        { "Pacific Islander", new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", 
                                   "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Christopher", "Karen", 
                                   "Charles", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Helen", "Mark", "Sandra", 
                                   "Tevin", "Kalani", "Kai", "Leilani", "Myles", "Moana", "Koa", "Anuenue", "Loa", "Malie", 
                                   "Ikaika", "Kailani", "Kelan", "Kaimana", "Makaio", "Kohana", "Ho'onani", "Mahina", "Kolomona", "Kaila" } },
        { "Middle Eastern", new[] { "Mohamed", "Fatima", "Ahmed", "Aisha", "Omar", "Khadija", "Ali", "Layla", "Hassan", "Zahra", 
                                 "Karim", "Nadia", "Youssef", "Maha", "Tariq", "Rania", "Sami", "Dalia", "Rashid", "Salma", 
                                 "Khalil", "Yasmin", "Farid", "Nour", "Bilal", "Huda", "Imran", "Soraya", "Nabil", "Ghada", 
                                 "Adnan", "Tamara", "Jamal", "Intisar", "Ossama", "Maysa", "Rami", "Rima", "Walid", "Samira" } },
        { "Multiracial", new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", 
                              "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Christopher", "Karen", 
                              "Charles", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Helen", "Mark", "Sandra", 
                              "Ashley", "Brandon", "Tyler", "Kayla", "Christian", "Taylor", "Dakota", "Jordan", "Morgan", "Alexis", 
                              "Cameron", "Morgan", "Drew", "Casey", "Jamie", "Riley", "Quinn", "Avery", "Peyton", "Sage" } }
    };

    private static readonly string[] LastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts",
        "Gomez", "Phillips", "Evans", "Turner", "Diaz", "Parker", "Cruz", "Edwards", "Collins", "Reyes",
        "Stewart", "Morris", "Morales", "Murphy", "Cook", "Rogers", "Gutierrez", "Ortiz", "Morgan", "Cooper",
        "Peterson", "Bailey", "Reed", "Kelly", "Howard", "Ramos", "Kim", "Cox", "Ward", "Richardson",
        "Watson", "Brooks", "Chavez", "Wood", "James", "Bennett", "Gray", "Mendoza", "Ruiz", "Hughes",
        "Price", "Alvarez", "Castillo", "Sanders", "Patel", "Myers", "Long", "Ross", "Foster", "Jimenez",
        "Chen", "Wells", "Fox", "Washington", "Butler", "Simmons", "Foster", "Gonzales", "Bryant", "Alexander",
        "Russell", "Griffin", "Diaz", "Hayes", "Ford", "Hamilton", "Graham", "Sullivan", "Wallace", "Coleman"
    };

    // ═══════════════════════════════════════════════════════════════
    // NON-GEOGRAPHIC FALLBACK DISTRIBUTIONS (used when county data is unavailable)
    // ═══════════════════════════════════════════════════════════════
    // These are only used as ultimate fallbacks. Primary data comes from CountyDemographicsDatabase.

    // Marital status (age-dependent anyway)
    private static readonly (string status, double weight)[] MaritalStatuses = new[]
    {
        ("Single", 0.30), ("Married", 0.45), ("Divorced", 0.15), ("Widowed", 0.07), ("Separated", 0.03)
    };

    // Parental status (age-dependent)
    private static readonly (string status, double weight)[] ParentalStatuses = new[]
    {
        ("No Children", 0.35), ("Has Children", 0.50), ("Empty Nester", 0.15)
    };

    // Media consumption (national patterns)
    private static readonly (string media, double weight)[] MediaConsumption = new[]
    {
        ("Mainstream News (Cable)", 0.35), ("Local News", 0.15), ("Social Media", 0.25),
        ("Online News Only", 0.15), ("Alternative Media", 0.05), ("Minimal News", 0.05)
    };

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    public List<Agent> GenerateJuryPanel(int jurorCount, int alternateCount, string county, string state = "South Carolina")
    {
        // Resolve jurisdiction demographics once for the entire panel
        _demos = CountyDemographicsDatabase.Resolve(county, state);

        var panel = new List<Agent>();

        for (int i = 0; i < jurorCount; i++)
        {
            var juror = GenerateJurorWithDemographics();
            juror.Role = AgentRole.Juror;
            panel.Add(juror);
        }

        for (int i = 0; i < alternateCount; i++)
        {
            var alternate = GenerateJurorWithDemographics();
            alternate.Role = AgentRole.AlternateJuror;
            panel.Add(alternate);
        }

        return panel;
    }

    public Agent GenerateJuror(string county, string state = "South Carolina")
    {
        _demos = CountyDemographicsDatabase.Resolve(county, state);
        return GenerateJurorWithDemographics();
    }

    /// <summary>
    /// Core juror generator using the currently resolved _demos.
    /// </summary>
    private Agent GenerateJurorWithDemographics()
    {
        int age = SampleAge(_demos);
        string race = SampleRace(_demos);
        string gender = SampleGender(_demos);

        var juror = new Agent
        {
            IsOccupied = true,
            Role = AgentRole.Juror,
            Name = GenerateName(gender, race),
            Gender = gender,
            Age = age,
            Race = race,
            EducationLevel = SampleEducation(age, _demos),
            IncomeLevel = SampleIncome(_demos),
            MaritalStatus = SampleMaritalStatus(age),
            ParentalStatus = SampleParentalStatus(age),
            ReligiousAffiliation = SampleReligion(_demos),
            PoliticalAffiliation = SamplePolitics(_demos),
            MediaConsumption = SampleMedia(),
            Occupation = SampleOccupation(age, _demos),
            Hobbies = SampleHobbies(age, gender),
            ConsumerSegment = SampleConsumerSegment(age),
            SpecializedKnowledge = SampleSpecializedKnowledge(age),
            ZipCode = GenerateZipCode(_demos),
            VerdictLean = ComputeInitialVerdictLean(age, race, gender),
            Bias = _random.NextDouble() * 0.4 - 0.2,
            Sentiment = 0.5 + (_random.NextDouble() * 0.2 - 0.1),
            RiskPerception = _random.NextDouble(),
            CurrentStatus = "Attentive",
            Valuation = "$0"
        };

        juror.Profile = GenerateProfile(juror);
        juror.EducationLevel = CoerceEducation(juror.EducationLevel, juror.Occupation, juror.Age);
        juror.SystemPrompt = BuildSystemPrompt(juror);

        return juror;
    }

    // ═══════════════════════════════════════════════════════════════
    // NAME GENERATION
    // ═══════════════════════════════════════════════════════════════
    private string GenerateName(string gender, string race)
    {
        // Determine which ethnic group to use based on race
        string ethnicGroup = race;
        
        // Map specific race values to broader groups for name selection
        if (race == "White" || race == "Black" || race == "Hispanic/Latino" || 
            race == "Asian" || race == "Native American" || race == "Pacific Islander" || 
            race == "Middle Eastern" || race == "Multiracial")
        {
            ethnicGroup = race;
        }
        else
        {
            // Default to White names if race is not in our predefined list
            ethnicGroup = "White";
        }

        // Get first names for the ethnic group
        string[] possibleFirstNames = FirstNameEthnicityMap.ContainsKey(ethnicGroup) 
            ? FirstNameEthnicityMap[ethnicGroup] 
            : FirstNameEthnicityMap["White"];

        // Select a random first name appropriate for gender if known
        string firstName;
        if (gender == "Female")
        {
            // Choose from female-appropriate names if possible
            var femaleNames = possibleFirstNames.Where((name, index) => index % 2 == 1).ToArray();
            firstName = femaleNames.Length > 0 ? femaleNames[_random.Next(femaleNames.Length)] : possibleFirstNames[_random.Next(possibleFirstNames.Length)];
        }
        else if (gender == "Male")
        {
            // Choose from male-appropriate names if possible
            var maleNames = possibleFirstNames.Where((name, index) => index % 2 == 0).ToArray();
            firstName = maleNames.Length > 0 ? maleNames[_random.Next(maleNames.Length)] : possibleFirstNames[_random.Next(possibleFirstNames.Length)];
        }
        else
        {
            // For non-binary/other, pick randomly
            firstName = possibleFirstNames[_random.Next(possibleFirstNames.Length)];
        }

        // Select a random last name
        string lastName = LastNames[_random.Next(LastNames.Length)];

        return $"{firstName} {lastName}";
    }

    // ═══════════════════════════════════════════════════════════════
    // DEMOGRAPHIC SAMPLING (jurisdiction-driven)
    // ═══════════════════════════════════════════════════════════════

    private int SampleAge(CountyDemographics demos)
    {
        var ranges = demos.AgeRanges.Count > 0 ? demos.AgeRanges
            : new() { (21,29,0.20),(30,39,0.25),(40,49,0.22),(50,59,0.18),(60,70,0.10),(71,85,0.05) };

        double totalWeight = ranges.Sum(r => r.Weight);
        double roll = _random.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (min, max, weight) in ranges)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return _random.Next(min, max + 1);
        }
        return _random.Next(30, 60);
    }

    private string SampleGender(CountyDemographics demos)
    {
        if (demos.GenderDistribution.Count > 0)
            return WeightedChoice(demos.GenderDistribution);
        return WeightedChoice(("Male",0.49),("Female",0.50),("Non-binary/Other",0.01));
    }

    private string SampleRace(CountyDemographics demos)
    {
        if (demos.RaceDistribution.Count > 0)
            return WeightedChoice(demos.RaceDistribution);
        return WeightedChoice(("White",0.60),("Black",0.13),("Hispanic/Latino",0.18),("Asian",0.06));
    }

    /// <summary>
    /// Computes a varied initial verdict lean (0=defense, 1=prosecution) based on demographics.
    /// Uses wider random variance to ensure genuine juror disagreement drives deliberation.
    /// </summary>
    private double ComputeInitialVerdictLean(int age, string race, string gender)
    {
        double lean = 0.5;
        lean += (age - 40) * 0.003;
        // Subtle gender effect — reduced from +0.04 to avoid all-female juries all leaning prosecution
        if (gender == "Female") lean += 0.01;
        else if (gender == "Male") lean -= 0.01;
        if (race == "Black") lean -= 0.06;
        else if (race == "Hispanic/Latino") lean -= 0.03;
        else if (race == "Asian") lean += 0.02;
        else if (race == "Native American") lean -= 0.04;
        // Wide random variance (±0.15) ensures genuine diversity of opinion —
        // some jurors naturally lean defense, others prosecution, creating real debate
        lean += (_random.NextDouble() * 0.30 - 0.15);
        return Math.Clamp(lean, 0.15, 0.85);
    }

    private string SampleEducation(int age, CountyDemographics demos)
    {
        // Start from county base distribution, then apply age constraints
        var baseDist = demos.EducationDistribution.Count > 0 ? demos.EducationDistribution
            : new() { ("High School",0.28),("Some College",0.28),("Associate Degree",0.10),("Bachelor's Degree",0.22),("Master's Degree",0.08),("Doctorate",0.02),("Trade School",0.02) };

        // Age-bracket adjustments: zero out weights for impossible education levels at this age
        var adjusted = baseDist.Select(e =>
        {
            double w = e.Weight;
            if (age < 18 && e.Label.Contains("Doctorate")) w = 0;
            if (age < 20 && (e.Label.Contains("Bachelor") || e.Label.Contains("Master") || e.Label.Contains("Doctorate") || e.Label.Contains("Associate"))) w = 0;
            if (age < 23 && (e.Label.Contains("Master") || e.Label.Contains("Doctorate"))) w = 0;
            if (age < 26 && e.Label.Contains("Doctorate")) w = 0;
            return (e.Label, w);
        }).ToList();

        double total = adjusted.Sum(a => a.w);
        if (total <= 0) return "High School";

        double roll = _random.NextDouble() * total;
        double cum = 0;
        foreach (var (label, w) in adjusted)
        {
            cum += w;
            if (roll <= cum) return label;
        }
        return "High School";
    }

    private string SampleIncome(CountyDemographics demos)
    {
        if (demos.IncomeDistribution.Count > 0)
            return WeightedChoice(demos.IncomeDistribution);
        return WeightedChoice(("Lower Class",0.15),("Working Class",0.20),("Middle Class",0.35),("Upper Middle Class",0.20),("Upper Class",0.10));
    }

    private string SampleReligion(CountyDemographics demos)
    {
        if (demos.ReligionDistribution.Count > 0)
            return WeightedChoice(demos.ReligionDistribution);
        return WeightedChoice(("Protestant",0.45),("Catholic",0.22),("Jewish",0.02),("Non-religious",0.20),("Other Christian",0.05));
    }

    private string SamplePolitics(CountyDemographics demos)
    {
        if (demos.PoliticalDistribution.Count > 0)
            return WeightedChoice(demos.PoliticalDistribution);
        return WeightedChoice(("Independent",0.42),("Democratic",0.30),("Republican",0.27));
    }

    private string SampleMedia() => WeightedChoice(MediaConsumption);

    private string GenerateZipCode(CountyDemographics demos)
    {
        return $"{_random.Next(demos.ZipMin, demos.ZipMax + 1):00000}";
    }

    /// <summary>
    /// Ensures education level is coherent with occupation AND age.
    /// No 18-year-old doctors, no plumbers with PhDs, etc.
    /// </summary>
    private static string CoerceEducation(string education, string occupation, int age)
    {
        // ── Age constraints first ──
        if (age < 20 && (education.Contains("Bachelor") || education.Contains("Master") 
                       || education.Contains("Doctorate") || education.Contains("PhD")
                       || education.Contains("Associate")))
            return "High School";

        if (age < 23 && (education.Contains("Master") || education.Contains("Doctorate") 
                       || education.Contains("PhD")))
            return "Bachelor's Degree";

        if (age < 26 && (education.Contains("Doctorate") || education.Contains("PhD")))
            return "Master's Degree";

        var occ = occupation.ToLowerInvariant();
        
        // High-education professional roles need at least a Bachelor's
        string[] highEdRoles = { "doctor", "surgeon", "physician", "lawyer", "attorney", "professor", 
                                 "dentist", "pharmacist", "veterinarian", "psychologist", "architect",
                                 "engineer", "scientist", "researcher", "physicist", "chemist", 
                                 "biologist", "judge", "executive", "cfo", "ceo", "cto" };
        string[] mediumEdRoles = { "teacher", "nurse", "accountant", "therapist", "counselor", "analyst",
                                   "programmer", "developer", "manager", "administrator", "paralegal" };
        string[] tradeRoles = { "plumber", "electrician", "mechanic", "carpenter", "welder", "mason",
                                "roofer", "painter", "landscaper", "hvac", "technician", "truck driver",
                                "construction", "machinist", "forklift", "assembler", "warehouse",
                                "maintenance", "custodian", "janitor", "gardener", "fisherman", 
                                "farm worker", "laborer", "cleaner", "housekeeper", "bartender", "barista" };
        string[] serviceRoles = { "waiter", "waitress", "server", "cashier", "clerk", "receptionist",
                                  "retail", "sales associate", "customer service", "security guard",
                                  "delivery driver", "cook", "chef", "dishwasher", "host", "hostess",
                                  "bellhop", "valet", "usher", "ticket", "call center" };

        bool hasLowEducation = education.Contains("High School") || education.Contains("Some College") 
                            || education.Contains("GED") || education.Contains("Trade School")
                            || education.Contains("Associate");
        bool hasHighEducation = education.Contains("Master") || education.Contains("Doctorate") 
                             || education.Contains("PhD") || education.Contains("Professional");

        // High-education roles: can't have low education
        foreach (var role in highEdRoles)
        {
            if (occ.Contains(role) && hasLowEducation)
                return age >= 30 ? "Master's Degree" : "Bachelor's Degree";
        }

        // Medium-education roles: can't have high school only
        foreach (var role in mediumEdRoles)
        {
            if (occ.Contains(role) && (education.Contains("High School") || education.Contains("GED")))
                return "Bachelor's Degree";
        }

        // Trade roles: shouldn't have advanced degrees (Master's, PhD)
        // Plumber with a Master's = incoherent
        foreach (var role in tradeRoles)
        {
            if (occ.Contains(role) && hasHighEducation)
                return "Trade School";
        }

        // Trade role with Bachelor's is plausible (e.g., "Plumber with Business degree") 
        // but plumber with Master's or PhD is not
        foreach (var role in tradeRoles)
        {
            if (occ.Contains(role) && education.Contains("Bachelor"))
                return education; // Bachelor's is OK for trades (career change, etc.)
        }

        // Service roles: shouldn't have advanced degrees
        foreach (var role in serviceRoles)
        {
            if (occ.Contains(role) && hasHighEducation)
                return age >= 25 ? "Some College" : "High School";
        }

        return education;
    }

    // ═══════════════════════════════════════════════════════════════
    // OCCUPATION GENERATION
    // ═══════════════════════════════════════════════════════════════

    private static readonly string[] OccupationsProfessional = new[]
    {
        "Accountant", "Engineer", "Teacher", "Nurse", "Manager", "Sales Representative",
        "Administrator", "Analyst", "Consultant", "IT Professional"
    };
    private static readonly string[] OccupationsTrade = new[]
    {
        "Electrician", "Plumber", "Mechanic", "Carpenter", "Construction Worker", "Truck Driver",
        "Police Officer", "Firefighter", "Security Guard", "Factory Worker"
    };
    private static readonly string[] OccupationsService = new[]
    {
        "Retail Worker", "Food Service", "Customer Service", "Administrative Assistant",
        "Cashier", "Janitor", "Home Health Aide", "Childcare Worker"
    };
    private static readonly string[] OccupationsRural = new[]
    {
        "Farmer", "Rancher", "Farm Hand", "Logger", "Agricultural Worker", "Equipment Operator"
    };

    private string SampleOccupation(int age, CountyDemographics demos)
    {
        if (age >= 65) return "Retired";
        if (age < 22) return "Student";

        // Build occupation pool weighted by urban/rural mix of the county
        var pool = new List<string>();
        pool.AddRange(OccupationsProfessional);
        pool.AddRange(OccupationsService);

        // Trade occupations more common in rural areas
        int tradeCount = demos.UrbanPercent < 0.5 ? 3 : 2;
        for (int i = 0; i < tradeCount; i++) pool.AddRange(OccupationsTrade);

        // Rural occupations appear only in rural counties
        if (demos.UrbanPercent < 0.6)
            pool.AddRange(OccupationsRural);

        return pool[_random.Next(pool.Count)];
    }

    // ═══════════════════════════════════════════════════════════════
    // MARITAL & PARENTAL STATUS (age-dependent, not geographic)
    // ═══════════════════════════════════════════════════════════════

    private string SampleMaritalStatus(int age)
    {
        if (age < 25) return "Single";
        if (age >= 60) return WeightedChoice(("Married",0.55),("Widowed",0.20),("Divorced",0.15),("Single",0.10));
        return WeightedChoice(MaritalStatuses);
    }

    private string SampleParentalStatus(int age)
    {
        if (age < 25) return "No Children";
        if (age > 55) return "Empty Nester";
        return WeightedChoice(ParentalStatuses);
    }

    // ═══════════════════════════════════════════════════════════════
    // HOBBIES, CONSUMER, KNOWLEDGE (non-geographic)
    // ═══════════════════════════════════════════════════════════════

    private static readonly string[] HobbyCategories = {
        "Sports & Fitness", "Arts & Crafts", "Reading & Writing", "Music & Performance",
        "Gaming & Technology", "Outdoors & Nature", "Cooking & Food", "Volunteering & Community",
        "Travel & Adventure", "Home & Garden", "Collecting & Antiques", "Pets & Animals"
    };

    private static readonly Dictionary<string, string[]> HobbiesByCategory = new()
    {
        { "Sports & Fitness", new[] { "Running", "Yoga", "Gym", "Basketball", "Swimming", "Cycling", "Hiking", "Golf", "Tennis", "Soccer", "Weightlifting", "Pilates" } },
        { "Arts & Crafts", new[] { "Painting", "Knitting", "Woodworking", "Pottery", "Scrapbooking", "Sewing", "Drawing", "Photography", "Jewelry making", "Calligraphy" } },
        { "Reading & Writing", new[] { "Reading fiction", "Reading non-fiction", "Writing", "Book clubs", "Poetry", "Journaling", "Blogging", "Crossword puzzles" } },
        { "Music & Performance", new[] { "Playing guitar", "Singing", "Piano", "Theater", "Dancing", "Choir", "Drums", "Vinyl collecting", "Concert going", "Karaoke" } },
        { "Gaming & Technology", new[] { "Video games", "Board games", "Coding", "Building PCs", "VR gaming", "Chess", "Card games", "Drone flying", "3D printing" } },
        { "Outdoors & Nature", new[] { "Fishing", "Hunting", "Camping", "Bird watching", "Kayaking", "Gardening", "Mountain biking", "Rock climbing", "Skiing", "Surfing" } },
        { "Cooking & Food", new[] { "Cooking", "Baking", "Grilling", "Wine tasting", "Brewing beer", "Restaurant exploring", "Meal prepping", "Farmers markets" } },
        { "Volunteering & Community", new[] { "Church volunteering", "Food bank", "Animal shelter", "Mentoring", "Neighborhood watch", "PTA", "Habitat for Humanity", "Red Cross" } },
        { "Travel & Adventure", new[] { "Road trips", "International travel", "National parks", "Cruises", "Backpacking", "RV camping", "Weekend getaways", "Sightseeing" } },
        { "Home & Garden", new[] { "Gardening", "Home improvement", "Interior design", "Lawn care", "DIY projects", "Houseplants", "Landscaping", "Furniture restoration" } },
        { "Collecting & Antiques", new[] { "Coin collecting", "Stamp collecting", "Antiquing", "Sports memorabilia", "Comic books", "Vintage cars", "Art collecting", "Comic cons" } },
        { "Pets & Animals", new[] { "Dog training", "Cat rescue", "Horseback riding", "Aquariums", "Beekeeping", "Bird keeping", "Animal fostering", "Pet photography" } }
    };

    private string SampleHobbies(int age, string gender)
    {
        // Pick 1-3 random hobby categories, then random hobbies from each
        int count = _random.Next(1, 4);
        var picked = new HashSet<string>();
        var selectedCategories = HobbyCategories.OrderBy(_ => _random.Next()).Take(count);

        var hobbies = new List<string>();
        foreach (var cat in selectedCategories)
        {
            if (HobbiesByCategory.TryGetValue(cat, out var options))
            {
                string hobby = options[_random.Next(options.Length)];
                if (picked.Add(hobby))
                    hobbies.Add(hobby);
            }
        }

        return string.Join(", ", hobbies.Count > 0 ? hobbies : new[] { "Reading", "Walking" });
    }

    private static readonly string[] ConsumerSegments = {
        "Budget Conscious", "Brand Loyal", "Early Adopter", "Mainstream Consumer",
        "Quality Focused", "Convenience Seeker", "Eco-Conscious", "Minimalist",
        "Status Seeker", "Family Focused", "Health Conscious", "Value Hunter"
    };

    private string SampleConsumerSegment(int age)
    {
        // Age-correlated segment preferences
        if (age > 60) return WeightedChoice(new[] { ("Budget Conscious", 0.25), ("Brand Loyal", 0.20), ("Quality Focused", 0.20), ("Mainstream Consumer", 0.15), ("Minimalist", 0.10), ("Value Hunter", 0.10) });
        if (age > 40) return WeightedChoice(new[] { ("Family Focused", 0.25), ("Quality Focused", 0.20), ("Brand Loyal", 0.15), ("Convenience Seeker", 0.15), ("Health Conscious", 0.15), ("Eco-Conscious", 0.10) });
        return WeightedChoice(ConsumerSegments.Select(s => (s, 1.0 / ConsumerSegments.Length)).ToArray());
    }

    private static readonly string[] SpecializedKnowledgeOptions = {
        "None (General Public)", "Basic Legal Knowledge", "Medical/Healthcare",
        "Technology/IT", "Finance/Accounting", "Engineering/Technical",
        "Education/Teaching", "Law Enforcement", "Military/Veteran",
        "Construction/Trades", "Business/Management", "Real Estate"
    };

    private string SampleSpecializedKnowledge(int age)
    {
        // Older jurors more likely to have specialized knowledge
        double specialistChance = age < 30 ? 0.3 : (age < 50 ? 0.5 : 0.7);
        if (_random.NextDouble() < specialistChance)
        {
            return SpecializedKnowledgeOptions[_random.Next(1, SpecializedKnowledgeOptions.Length)];
        }
        return "None (General Public)";
    }

    private string GenerateProfile(Agent juror)
    {
        string hobbiesText = string.IsNullOrWhiteSpace(juror.Hobbies) ? "Reading, Walking" : juror.Hobbies;
        string knowledgeText = string.IsNullOrWhiteSpace(juror.SpecializedKnowledge) ? "General public knowledge" : juror.SpecializedKnowledge;

        return $"A {juror.Age}-year-old {juror.Race} {juror.Gender} from {juror.ZipCode}. " +
               $"Education: {juror.EducationLevel}. Occupation: {juror.Occupation}. " +
               $"Marital: {juror.MaritalStatus}. Parental: {juror.ParentalStatus}. " +
               $"Income: {juror.IncomeLevel}. Politics: {juror.PoliticalAffiliation}. " +
               $"Religion: {juror.ReligiousAffiliation}. Media: {juror.MediaConsumption}. " +
               $"Consumer: {juror.ConsumerSegment}. " +
               $"Hobbies: {hobbiesText}. Knowledge: {knowledgeText}.";
    }

    private string BuildSystemPrompt(Agent juror)
    {
        return $"You are a juror named {juror.Name}. {juror.Profile} " +
               $"Your hobbies include {juror.Hobbies}. " +
               $"As a {juror.Occupation}, you have specialized knowledge in {juror.SpecializedKnowledge}. " +
               "You are participating in a courtroom simulation. " +
               "Consider the evidence and arguments presented, and form an opinion based on your " +
               "background, experiences, and the facts of the case. " +
               "Your verdict lean should reflect your genuine assessment.";
    }

    private string WeightedChoice(params (string, double)[] options)
    {
        double totalWeight = options.Sum(o => o.Item2);
        double roll = _random.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (value, weight) in options)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return value;
        }
        return options[0].Item1;
    }

    private string WeightedChoice(List<(string Label, double Weight)> options)
    {
        double totalWeight = options.Sum(o => o.Weight);
        double roll = _random.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (label, weight) in options)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return label;
        }
        return options[0].Label;
    }
}
