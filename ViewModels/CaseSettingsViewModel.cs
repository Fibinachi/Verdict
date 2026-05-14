using System;
using System.Collections.Generic;
using System.Linq;
using Verdict.Models;

namespace Verdict.ViewModels
{
    public class CaseSettingsViewModel : ViewModelBase
    {
        private CaseFile _caseFile;
        private bool _isDefaultSettings;

        public CaseFile CaseFile
        {
            get => _caseFile;
            set => SetProperty(ref _caseFile, value);
        }

        public bool IsDefaultSettings
        {
            get => _isDefaultSettings;
            set => SetProperty(ref _isDefaultSettings, value);
        }

        public IEnumerable<CaseMode> CaseModes => Enum.GetValues(typeof(CaseMode)).Cast<CaseMode>();
        public IEnumerable<JurisdictionType> JurisdictionTypes => Enum.GetValues(typeof(JurisdictionType)).Cast<JurisdictionType>();
        public IEnumerable<TrialPhase> TrialPhases => Enum.GetValues(typeof(TrialPhase)).Cast<TrialPhase>();

        public CaseSettingsViewModel(CaseFile caseFile, bool isDefaultSettings = false)
        {
            // Handle the case where caseFile might be null
            _caseFile = caseFile ?? new CaseFile();
            _caseFile.AddDefaultModels();
            _isDefaultSettings = isDefaultSettings;
        }
    }
}