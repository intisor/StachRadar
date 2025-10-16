namespace StackRadar.Core.Detection;

public enum DetectionSignal
{
    HeaderAspNetVersion,
    HeaderPoweredByAspNet,
    HeaderIisServer,
    HtmlViewState,
    HtmlCshtmlReference,
    HtmlRazorDirective,
    CookieAspNet,
    CookieAspNetCore,
    FinalUrlAspx,
    EnrichmentWhoisMicrosoft,
    EnrichmentGitHubMatch,
    BuiltWithAspNet
}
