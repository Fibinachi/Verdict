using System.Text;
using Verdict.Models;

namespace Verdict.Services
{
    public class NeutralSummaryService
    {
        public static string GenerateNeutralSummary(CaseFile caseFile)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NEUTRAL SUMMARY");
            sb.AppendLine("===============");
            sb.AppendLine();

            foreach (var doc in caseFile.Evidence)
            {
                sb.AppendLine($"Evidence: {doc.FileName}");
                sb.AppendLine($"Maximum Damages: {doc.EstimatedDamages:C}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}