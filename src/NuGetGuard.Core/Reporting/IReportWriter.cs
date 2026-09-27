using NuGetGuard.Core.Models;

namespace NuGetGuard.Core.Reporting;

public interface IReportWriter
{
    void Write(ScanResult result, TextWriter output);
}
