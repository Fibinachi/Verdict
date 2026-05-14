using System;

namespace Verdict.Models
{
    /// <summary>
    /// Represents an opinion that one juror holds about another juror.
    /// Opinions are shaped by personality traits and biases.
    /// </summary>
    public class JurorOpinion : ObservableObject
    {
        private Guid _sourceAgentId;
        private Guid _targetAgentId;
        private double _strength;
        private double _biasInfluence;
        private string _description = string.Empty;

        /// <summary>
        /// The unique identifier of the juror who holds this opinion.
        /// </summary>
        public Guid SourceAgentId
        {
            get => _sourceAgentId;
            set => SetProperty(ref _sourceAgentId, value);
        }

        /// <summary>
        /// The unique identifier of the juror being evaluated.
        /// </summary>
        public Guid TargetAgentId
        {
            get => _targetAgentId;
            set => SetProperty(ref _targetAgentId, value);
        }

        /// <summary>
        /// How strongly this opinion is held (0.0 to 1.0).
        /// </summary>
        public double Strength
        {
            get => _strength;
            set => SetProperty(ref _strength, Math.Clamp(value, 0.0, 1.0));
        }

        /// <summary>
        /// Influence of holder's bias on opinion formation (-1 to 1).
        /// </summary>
        public double BiasInfluence
        {
            get => _biasInfluence;
            set => SetProperty(ref _biasInfluence, Math.Clamp(value, -1.0, 1.0));
        }

        /// <summary>
        /// Text explanation of why this opinion exists.
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value ?? string.Empty);
        }
    }
}