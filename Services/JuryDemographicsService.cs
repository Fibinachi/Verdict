using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.Services;

/// <summary>
/// Service for generating jury pools based on realistic demographic distributions
/// derived from US Census data patterns for specific counties and regions.
/// Default bias factor weights are based on juror bias research documented in
/// docs/JurorBiasResearch.md
/// </summary>
public interface IJuryDemographicsService
{
    /// <summary>
    /// Generates a jury panel with the specified number of jurors and alternates.
    /// </summary>
    /// <param name="jurorCount">Number of regular jurors (typically 12)</param>
    /// <param name="alternateCount">Number of alternate jurors (typically 2)</param>
    /// <param name="county">County name for demographic calibration (e.g., "Richland County")</param>
    /// <param name="state">State for regional demographic patterns</param>
    /// <returns>List of Agent objects representing the jury panel</returns>
    List<Agent> GenerateJuryPanel(int jurorCount, int alternateCount, string county, string state = "South Carolina");

    /// <summary>
    /// Generates a single juror with demographics drawn from population distributions.
    /// </summary>
    Agent GenerateJuror(string county, string state = "South Carolina");
}

/// <summary>
/// Implementation of jury demographics generation using probabilistic models
/// based on US Census data patterns.
/// </summary>
public class JuryDemographicsService : IJuryDemographicsService
{
    private static readonly Random _random = new();

    // Name lists by ethnicity/race to make names match demographics
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

    // Age group probabilities (weighted)
    private static readonly (int min, int max, double weight)[] AgeRanges = new[]
    {
        (21, 29, 0.20),
        (30, 39, 0.25),
        (40, 49, 0.22),
        (50, 59, 0.18),
        (60, 70, 0.10),
        (71, 85, 0.05)
    };

    // Gender distribution (approximate US population)
    private static readonly (string gender, double weight)[] Genders = new[]
    {
        ("Male", 0.49),
        ("Female", 0.50),
        ("Non-binary/Other", 0.01)
    };

    // Race/ethnicity distribution (varies by region; using national averages as baseline)
    private static readonly (string race, double weight)[] Races = new[]
    {
        ("White", 0.60),
        ("Black", 0.13),
        ("Hispanic/Latino", 0.18),
        ("Asian", 0.06),
        ("Native American", 0.02),
        ("Pacific Islander", 0.005),
        ("Middle Eastern", 0.005),
        ("Multiracial", 0.025)
    };

    // Education level distribution (juror pool typically 25+)
    private static readonly (string education, double weight)[] EducationLevels = new[]
    {
        ("High School", 0.28),
        ("Some College", 0.28),
        ("Associate Degree", 0.10),
        ("Bachelor's Degree", 0.22),
        ("Master's Degree", 0.08),
        ("Doctorate", 0.02),
        ("Trade School", 0.02)
    };

    // Income level distribution (household income)
    private static readonly (string income, double weight)[] IncomeLevels = new[]
    {
        ("Lower Class", 0.15),
        ("Working Class", 0.20),
        ("Middle Class", 0.35),
        ("Upper Middle Class", 0.20),
        ("Upper Class", 0.10)
    };

    // Common occupations by category
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

    private static readonly string[] OccupationsRetired = new[]
    {
        "Retired", "Retired Teacher", "Retired Engineer", "Retired Nurse", "Retired Manager"
    };

    private static readonly string[] OccupationsStudents = new[]
    {
        "Student", "Graduate Student", "University Student"
    };

    // Marital status distribution
    private static readonly (string status, double weight)[] MaritalStatuses = new[]
    {
        ("Single", 0.30),
        ("Married", 0.45),
        ("Divorced", 0.15),
        ("Widowed", 0.07),
        ("Separated", 0.03)
    };

    // Parental status (dependent on age)
    private static readonly (string status, double weight)[] ParentalStatuses = new[]
    {
        ("No Children", 0.35),
        ("Has Children", 0.50),
        ("Empty Nester", 0.15)
    };

    // Religious affiliation
    private static readonly (string religion, double weight)[] Religions = new[]
    {
        ("Protestant", 0.45),
        ("Catholic", 0.22),
        ("Jewish", 0.02),
        ("Muslim", 0.01),
        ("Buddhist", 0.01),
        ("Hindu", 0.01),
        ("Non-religious", 0.20),
        ("Other Christian", 0.05),
        ("Other Faith", 0.03)
    };

    // Political affiliation (moderate distribution)
    private static readonly (string party, double weight)[] PoliticalAffiliation = new[]
    {
        ("Independent", 0.42),
        ("Democratic", 0.30),
        ("Republican", 0.27),
        ("Other/None", 0.01)
    };

    // Media consumption patterns
    private static readonly (string media, double weight)[] MediaConsumption = new[]
    {
        ("Mainstream News (Cable)", 0.35),
        ("Local News", 0.15),
        ("Social Media", 0.25),
        ("Online News Only", 0.15),
        ("Alternative Media", 0.05),
        ("Minimal News", 0.05)
    };

    public List<Agent> GenerateJuryPanel(int jurorCount, int alternateCount, string county, string state = "South Carolina")
    {
        var panel = new List<Agent>();

        // Generate regular jurors
        for (int i = 0; i < jurorCount; i++)
        {
            var juror = GenerateJuror(county, state);
            juror.Role = AgentRole.Juror;
            juror.Name = $"Juror {i + 1}";
            panel.Add(juror);
        }

        // Generate alternates
        for (int i = 0; i < alternateCount; i++)
        {
            var alternate = GenerateJuror(county, state);
            alternate.Role = AgentRole.AlternateJuror;
            alternate.Name = $"Alt {i + 1}";
            panel.Add(alternate);
        }

        return panel;
    }

    public Agent GenerateJuror(string county, string state = "South Carolina")
    {
        int age = SampleAge();
        string race = SampleRace();
        string gender = SampleGender();
        
        var juror = new Agent
        {
            IsOccupied = true,
            Role = AgentRole.Juror,
            Gender = gender,
            Age = age,
            Race = race,
            EducationLevel = SampleEducation(age),
            IncomeLevel = SampleIncome(),
            MaritalStatus = SampleMaritalStatus(age),
            ParentalStatus = SampleParentalStatus(age),
            ReligiousAffiliation = SampleReligion(),
            PoliticalAffiliation = SamplePolitics(),
            MediaConsumption = SampleMedia(),
            Occupation = SampleOccupation(age),
            ZipCode = GenerateZipCode(county, state),
            VerdictLean = 0.5, // Neutral starting point
            Bias = _random.NextDouble() * 0.4 - 0.2, // Small random bias (-0.2 to +0.2)
            Sentiment = 0.5,
            CurrentStatus = "Attentive",
            // Generate a realistic name based on demographics
            Name = GenerateName(gender, race)
        };

        // Generate a profile based on demographics
        juror.Profile = GenerateProfile(juror);
        juror.SystemPrompt = BuildSystemPrompt(juror);

        return juror;
    }

    /// <summary>
    /// Generates a realistic name based on gender and race/ethnicity
    /// </summary>
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

    private int SampleAge()
    {
        var ranges = AgeRanges;
        double totalWeight = ranges.Sum(r => r.weight);
        double roll = _random.NextDouble() * totalWeight;

        double cumulative = 0;
        foreach (var (min, max, weight) in ranges)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return _random.Next(min, max + 1);
            }
        }
        return _random.Next(30, 60);
    }

    private string SampleGender() => WeightedChoice(Genders);
    private string SampleRace() => WeightedChoice(Races);

    private string SampleEducation(int age)
    {
        // Education correlated with age
        var educationWeights = EducationLevels.ToArray();
        if (age < 25)
        {
            // Younger: more students, less advanced degrees
            educationWeights = new (string, double)[]
            {
                ("High School", 0.35),
                ("Some College", 0.40),
                ("Associate Degree", 0.10),
                ("Bachelor's Degree", 0.10),
                ("Master's Degree", 0.02),
                ("Doctorate", 0.0),
                ("Trade School", 0.03)
            };
        }
        return WeightedChoice(educationWeights);
    }

    private string SampleIncome()
    {
        // Income should correlate somewhat with education (handled in profile generation)
        return WeightedChoice(IncomeLevels);
    }

    private string SampleMaritalStatus(int age)
    {
        if (age < 25) return "Single";
        if (age >= 60) return WeightedChoice(new[] { ("Married", 0.55), ("Widowed", 0.20), ("Divorced", 0.15), ("Single", 0.10) });
        return WeightedChoice(MaritalStatuses);
    }

    private string SampleParentalStatus(int age)
    {
        if (age < 25) return "No Children";
        if (age > 55) return "Empty Nester";
        return WeightedChoice(ParentalStatuses);
    }

    private string SampleReligion() => WeightedChoice(Religions);
    private string SamplePolitics() => WeightedChoice(PoliticalAffiliation);
    private string SampleMedia() => WeightedChoice(MediaConsumption);

    private string SampleOccupation(int age)
    {
        if (age >= 65) return "Retired";
        if (age < 22) return "Student";

        var allOccupations = OccupationsProfessional
            .Concat(OccupationsTrade)
            .Concat(OccupationsService)
            .Concat(OccupationsRetired.Where(o => age < 60)) // Some early retirees
            .Concat(OccupationsStudents.Where(o => age < 30))
            .ToList();

        // Weight professional occupations higher for higher education
        var weights = allOccupations.Select(o => 1.0).ToList();
        return allOccupations[_random.Next(allOccupations.Count)];
    }

    private string GenerateZipCode(string county, string state)
    {
        // Simple placeholder ZIP generator (could map to real county ZIP ranges)
        // For Richland County, SC: ranges 29201-29209, 29223, 29229, 29250, 29260, 29290
        string stateAbbr = state.Substring(0, 2).ToUpper();
        return $"{_random.Next(29000, 29999):00000}";
    }

    private string GenerateProfile(Agent juror)
    {
        // Generate hobbies based on age, occupation, and education
        var hobbies = SampleHobbies(juror.Age, juror.Occupation, juror.EducationLevel);
        juror.Hobbies = string.Join(", ", hobbies);

        // Generate specialized knowledge based on occupation
        juror.SpecializedKnowledge = SampleSpecializedKnowledge(juror.Occupation);

        return $"A {juror.Age}-year-old {juror.Race} {juror.Gender} from {juror.ZipCode}. " +
               $"Education: {juror.EducationLevel}. Occupation: {juror.Occupation}. " +
               $"Marital: {juror.MaritalStatus}. Parental: {juror.ParentalStatus}. " +
               $"Income: {juror.IncomeLevel}. Politics: {juror.PoliticalAffiliation}. " +
               $"Religion: {juror.ReligiousAffiliation}. Media: {juror.MediaConsumption}. " +
               $"Hobbies: {juror.Hobbies}. Knowledge: {juror.SpecializedKnowledge}.";
    }

    // Helper to generate a realistic hobby list
    private List<string> SampleHobbies(int age, string occupation, string education)
    {
        var possible = new List<string>();

        // Age‑based hobbies
        if (age < 30)
        {
            possible.AddRange(new[] { "Video games", "Traveling", "Hiking", "Fitness" });
        }
        else if (age >= 60)
        {
            possible.AddRange(new[] { "Gardening", "Reading", "Birdwatching", "Traveling" });
        }
        else
        {
            possible.AddRange(new[] { "Reading", "Cooking", "Fitness", "Traveling" });
        }

        // Occupation‑based hobbies
        if (occupation.Contains("Engineer") || occupation.Contains("IT"))
            possible.AddRange(new[] { "Programming", "Electronics", "Robotics" });
        if (occupation.Contains("Plumber") || occupation.Contains("Construction") || occupation.Contains("Carpenter"))
            possible.AddRange(new[] { "DIY projects", "Woodworking", "Home improvement" });
        if (occupation.Contains("Doctor") || occupation.Contains("Nurse"))
            possible.AddRange(new[] { "Medical podcasts", "Running", "Health forums" });
        if (occupation.Contains("Lawyer") || occupation.Contains("Attorney"))
            possible.AddRange(new[] { "Reading legal thrillers", "Debating", "Chess" });

        // Education‑based refinement
        if (education.Contains("Doctorate"))
            possible.AddRange(new[] { "Research", "Academic conferences" });
        if (education.Contains("High School"))
            possible.AddRange(new[] { "Sports", "Video games" });

        // Randomly pick up to 3 unique hobbies
        var selected = new HashSet<string>();
        while (selected.Count < 3 && possible.Count > 0)
        {
            var h = possible[_random.Next(possible.Count)];
            selected.Add(h);
        }
        return selected.ToList();
    }

    // Helper to map occupations to domain knowledge
    private string SampleSpecializedKnowledge(string occupation)
    {
        if (string.IsNullOrEmpty(occupation)) return "General public knowledge";
        var lowered = occupation.ToLower();
        if (lowered.Contains("plumber") || lowered.Contains("construction"))
            return "Plumbing and construction codes, home repair";
        if (lowered.Contains("doctor") || lowered.Contains("nurse"))
            return "Medical terminology, healthcare procedures";
        if (lowered.Contains("lawyer") || lowered.Contains("attorney"))
            return "Legal procedures, case law, courtroom etiquette";
        if (lowered.Contains("engineer"))
            return "Engineering principles, technical analysis";
        if (lowered.Contains("teacher"))
            return "Educational theory, pedagogy";
        return "General public knowledge";
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
}
