using System.Collections.ObjectModel;

namespace StackRadar.Core.Detection;

public sealed class DetectionOptions
{
    public int MinimumScoreForTrue { get; set; } = 7;
    public int MinimumScoreForBorderline { get; set; } = 4;
    public int MaxHtmlBytes { get; set; } = 256 * 1024;

    public IDictionary<DetectionSignal, int> SignalWeights { get; }
        = new ReadOnlyDictionary<DetectionSignal, int>(new Dictionary<DetectionSignal, int>
        {
            [DetectionSignal.HeaderAspNetVersion] = 4,
            [DetectionSignal.HeaderPoweredByAspNet] = 3,
            [DetectionSignal.HeaderIisServer] = 2,
            [DetectionSignal.CookieAspNet] = 3,
            [DetectionSignal.CookieAspNetCore] = 3,
            [DetectionSignal.HtmlViewState] = 4,
            [DetectionSignal.HtmlCshtmlReference] = 2,
            [DetectionSignal.HtmlRazorDirective] = 3,
            [DetectionSignal.FinalUrlAspx] = 2,
            [DetectionSignal.EnrichmentWhoisMicrosoft] = 1,
            [DetectionSignal.EnrichmentGitHubMatch] = 1
        });
}
