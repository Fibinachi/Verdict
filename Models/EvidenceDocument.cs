using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Verdict.Models;

public class EvidenceDocument : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _fileName = string.Empty;
    public string FileName
    {
        get => _fileName;
        set { if (_fileName != value) { _fileName = value; OnPropertyChanged(); } }
    }

    private string _filePath = string.Empty;
    public string FilePath
    {
        get => _filePath;
        set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); } }
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set { if (_summary != value) { _summary = value; OnPropertyChanged(); } }
    }

    private DateTime _admittedAt = DateTime.Now;
    public DateTime AdmittedAt
    {
        get => _admittedAt;
        set { if (_admittedAt != value) { _admittedAt = value; OnPropertyChanged(); } }
    }


    // Exhibit tracking for court record
    private int _exhibitNumber;
    public int ExhibitNumber
    {
        get => _exhibitNumber;
        set { if (_exhibitNumber != value) { _exhibitNumber = value; OnPropertyChanged(); } }
    }

    // Closing argument fields
    private string _closingArgumentPlaintiff = string.Empty;
    public string ClosingArgumentPlaintiff
    {
        get => _closingArgumentPlaintiff;
        set { if (_closingArgumentPlaintiff != value) { _closingArgumentPlaintiff = value; OnPropertyChanged(); } }
    }

    private string _closingArgumentDefense = string.Empty;
    public string ClosingArgumentDefense
    {
        get => _closingArgumentDefense;
        set { if (_closingArgumentDefense != value) { _closingArgumentDefense = value; OnPropertyChanged(); } }
    }

    private List<int> _referencedExhibitsPlaintiff = new();
    public List<int> ReferencedExhibitsPlaintiff
    {
        get => _referencedExhibitsPlaintiff;
        set { if (!ReferenceEquals(_referencedExhibitsPlaintiff, value)) { _referencedExhibitsPlaintiff = value; OnPropertyChanged(); } }
    }

    private List<int> _referencedExhibitsDefense = new();
    public List<int> ReferencedExhibitsDefense
    {
        get => _referencedExhibitsDefense;
        set { if (!ReferenceEquals(_referencedExhibitsDefense, value)) { _referencedExhibitsDefense = value; OnPropertyChanged(); } }
    }

    // Evidence strength (0.0 = weak, 1.0 = strong) - affects verdict lean
    private double _evidenceStrength = 0.5;
    public double EvidenceStrength
    {
        get => _evidenceStrength;
        set { if (Math.Abs(_evidenceStrength - value) > double.Epsilon) { _evidenceStrength = value; OnPropertyChanged(); } }
    }

    // Estimated damages value for settlement calculations
    private double _estimatedDamages;
    public double EstimatedDamages
    {
        get => _estimatedDamages;
        set { if (Math.Abs(_estimatedDamages - value) > double.Epsilon) { _estimatedDamages = value; OnPropertyChanged(); } }
    }

    // Whether evidence has been assessed in discovery
    private bool _isDiscoveryComplete;
    public bool IsDiscoveryComplete
    {
        get => _isDiscoveryComplete;
        set { if (_isDiscoveryComplete != value) { _isDiscoveryComplete = value; OnPropertyChanged(); } }
    }

    // Clerk notes for exhibit management
    private string? _clerkNotes;
    public string? ClerkNotes
    {
        get => _clerkNotes;
        set { if (_clerkNotes != value) { _clerkNotes = value; OnPropertyChanged(); } }
    }

    // Media type classification: "Image", "Document", etc.
    private string? _mediaType;
    public string? MediaType
    {
        get => _mediaType;
        set { if (_mediaType != value) { _mediaType = value; OnPropertyChanged(); } }
    }

    // Full detailed LLM analysis of the document (preserved separately from the summary)
    private string? _detailedAnalysis;
    public string? DetailedAnalysis
    {
        get => _detailedAnalysis;
        set { if (_detailedAnalysis != value) { _detailedAnalysis = value; OnPropertyChanged(); } }
    }

    // The attorney who offered this exhibit into evidence
    private string _offeringAttorney = string.Empty;
    public string OfferingAttorney
    {
        get => _offeringAttorney;
        set { if (_offeringAttorney != value) { _offeringAttorney = value; OnPropertyChanged(); } }
    }

    // Indexing for future usage
    private int _plaintiffSideExhibitNumber;
    public int PlaintiffSideExhibitNumber
    {
        get => _plaintiffSideExhibitNumber;
        set { if (_plaintiffSideExhibitNumber != value) { _plaintiffSideExhibitNumber = value; OnPropertyChanged(); } }
    }

    private int _defenseSideExhibitNumber;
    public int DefenseSideExhibitNumber
    {
        get => _defenseSideExhibitNumber;
        set { if (_defenseSideExhibitNumber != value) { _defenseSideExhibitNumber = value; OnPropertyChanged(); } }
    }

    // True if this exhibit is offered by the plaintiff side (civil plaintiff or prosecution)
    private bool _isOfferedByPlaintiffSide;
    public bool IsOfferedByPlaintiffSide
    {
        get => _isOfferedByPlaintiffSide;
        set { if (_isOfferedByPlaintiffSide != value) { _isOfferedByPlaintiffSide = value; OnPropertyChanged(); } }
    }
}

