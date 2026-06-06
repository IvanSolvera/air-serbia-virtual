using Microsoft.AspNetCore.Components.Web;

namespace AirSerbiaVirtua.Web.Client.Shared;

/// <summary>
/// Render-mode for portal pages: interactive WASM with prerendering disabled —
/// the session lives in localStorage, which only exists in the browser, so a
/// server prerender pass would always paint the signed-out state first.
/// </summary>
public static class PortalRender
{
    public static readonly InteractiveWebAssemblyRenderMode NoPrerender = new(prerender: false);
}
