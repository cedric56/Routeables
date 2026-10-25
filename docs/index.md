---
_layout: landing
title: Routeables
---

<div class="rt-hero"><div class="rt-hero-inner">
<div>

# Routing from your view models

One `IRouter` for Blazor, .NET MAUI, Avalonia, Uno Platform and WPF. Declare a route with an attribute; your view model never touches a page, a frame or a framework navigation API.

<div class="rt-cta">
<a class="rt-primary" href="getting-started.md">Get started</a>
<a class="rt-ghost" href="https://cedric56.github.io/Routeables/demo/avalonia/">Try the demo</a>
<a class="rt-ghost" href="guides/migrating-from-prism.md">Coming from Prism</a>
<a class="rt-ghost" href="https://github.com/cedric56/Routeables">GitHub</a>
</div>

</div>
<div class="rt-code"><header>HelloViewModel.cs</header><pre><code><span class="c">// the route lives on the view model</span>
<span class="k">[Routeable(</span><span class="s">"/hello/{name}"</span><span class="k">)]</span>
public sealed class HelloViewModel(IRouter router, string name)
{
    public Task SayHelloAgainAsync()
        =&gt; router.GoToAsync(<span class="s">"/hello/World"</span>);
}

<span class="c">// the view is found by its name</span>
public partial class HelloView : RouteableFor&lt;HelloViewModel&gt; { }

<span class="c">// once, at startup</span>
services.AddGeneratedRoutes();

<span class="c">// MainView: the zone where pages appear</span>
<span class="k">&lt;routeables:RouteableRegion</span> <span class="k">/&gt;</span></code></pre></div>
</div></div>

<ul class="rt-hosts">
<li><a href="guides/hosts.md">Blazor</a></li>
<li><a href="guides/hosts.md">.NET MAUI</a></li>
<li><a href="guides/hosts.md">Avalonia</a></li>
<li><a href="guides/hosts.md">Uno Platform</a></li>
<li><a href="guides/hosts.md">WPF</a></li>
</ul>

<div class="rt-grid">
<div><h3>Routes from attributes</h3><p>Paired with views by name and registered by a source generator. Nothing scans assemblies at run time.</p></div>
<div><h3>Typed routing</h3><p>A method generated from a constructor: <code>GoToMonkeyDetailAsync(router, name)</code> instead of a string.</p></div>
<div><h3>History and guards</h3><p>Back, forward, pop, root. A page can veto leaving.</p></div>
<div><h3>Regions and modules</h3><p>Several areas of the screen with their own history; a feature is one extension method.</p></div>
<div><h3>AOT and trimming</h3><p>Compatible by design, on every host.</p></div>
<div><h3>Same code everywhere</h3><p>One view model, five hosts, one Animals sample on each.</p></div>
</div>

- [Introduction](introduction.md): what it does and how the projects fit together.
- [Getting started](getting-started.md): install, declare routes, set up a host, route.
- [Guides](guides/routes.md): routes, typed routing, arguments, lifecycle, history, regions, hosts.
- [Benchmarks](benchmarks.md) and [host scenarios](host-scenarios.md): how fast the router is, and what to check on each platform.
- [API reference](api/Routeables.Abstraction.yml): every public type.

Source code and issues: [github.com/cedric56/Routeables](https://github.com/cedric56/Routeables).
