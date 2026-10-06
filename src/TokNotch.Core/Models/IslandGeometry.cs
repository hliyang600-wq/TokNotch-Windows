namespace TokNotch.Core.Models;

/// <summary>The fixed host reserves transparent space for a bouncing outline; the text canvas never scales.</summary>
public static class IslandGeometry
{
 public const double HostWidth=404,HostHeight=240;
 public const double ExpandedWidth=380,ExpandedHeight=220;
 public const double CollapsedWidth=180,CollapsedHeight=32;
 public const double MinimumSpringProgress=-.04,MaximumSpringProgress=1.08;
}
