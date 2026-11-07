using Routeables.Routing;
using Routeables.Segments;
using Routeables.Services;
using Routeables.Test.Abstraction;
using static Routeables.Test.Abstraction.RouterTestKit;
using Page = Routeables.Test.Abstraction.Page;

namespace Routeables.Tests.Routing;

// Random sequences with a fixed seed (the same run every time), checked against a simple model: a failure names the seed and the
// operations that led to it. They exist to catch what hand-written cases do not think of.

/// <summary>Random addresses built and read back: what was put in a route comes out of it, whatever the characters.</summary>
public class RandomizedRouteAndStateTests
{
    static readonly string[] Pieces = ["a", "Z", " ", "/", "?", "&", "=", "%", "%2F", "+", "#", "{", "}", "é", "日本", "..", ".", "~", ":", ";", "\\", "\"", "'", "\n", "%20", "0", "-", "_", "<", ">", "|", "@", "!", "*", "(", ")", "$", ",", "\t"];

    static string Rand(Random r, int max = 6)
    {
        var n = r.Next(1, max);
        var s = "";
        for (var i = 0; i < n; i++) s += Pieces[r.Next(Pieces.Length)];
        return s;
    }

    [Fact]
    public void Build_then_match_gives_back_the_values()
    {
        var r = new Random(12345);
        var bad = new List<string>();
        string[] templates = ["/items/{name}", "/items/{name}/edit", "/search?{q}", "/s/{name}?{q?}", "/x/{id:int}?{tag?}", "/files/{**path}"];
        for (var i = 0; i < 4000; i++)
        {
            var t = templates[r.Next(templates.Length)];
            var vals = new Dictionary<string, object?>();
            if (t.Contains("{name}")) vals["name"] = Rand(r);
            if (t.Contains("{q}") || t.Contains("{q?}")) vals["q"] = Rand(r);
            if (t.Contains("{tag?}")) vals["tag"] = Rand(r);
            if (t.Contains("{id:int}")) vals["id"] = r.Next(-5, 100000);
            if (t.Contains("{**path}")) vals["path"] = Rand(r, 4).Replace("//", "/");
            string route;
            try { route = RouteParser.Build(t, vals); } catch (Exception e) { bad.Add($"BUILD {t} {Show(vals)} -> {e.GetType().Name}"); continue; }
            var args = RouteableArgs.Create();
            var m = RouteParser.Mismatch(t, route, args);
            if (m is not null) { bad.Add($"MISMATCH {t} {Show(vals)} route={route} : {m}"); continue; }
            foreach (var (k, v) in vals)
            {
                if (k == "path") continue;
                var got = args != null && args.ViewModelParameters != null && args.ViewParameters != null ? 
                    args.ViewModelParameters.Concat(args.ViewParameters).FirstOrDefault(p => p.name == k)?.value : null;
                if (!Equals(got?.ToString(), v?.ToString())) bad.Add($"VALUE {t} {k}: expected [{v}] got [{got}] route={route}");
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Distinct().Take(15)) + $"\n(total {bad.Count})");
    }

    static string Show(Dictionary<string, object?> d) => string.Join(",", d.Select(p => $"{p.Key}=[{p.Value}]"));

    [Fact]
    public void State_serialization_round_trips()
    {
        var r = new Random(777);
        var bad = new List<string>();
        for (var i = 0; i < 2000; i++)
        {
            var snaps = new List<RouteableSnapshot>();
            for (var j = 0; j < r.Next(0, 4); j++)
            {
                var st = new Dictionary<string, string>();
                for (var k = 0; k < r.Next(0, 3); k++) st[Rand(r)] = Rand(r);
                snaps.Add(new RouteableSnapshot("/" + Rand(r), st));
            }
            var text = RouteableState.Serialize(snaps);
            var back = RouteableState.DeserializeSnapshots(text);
            if (!snaps.SequenceEqual(back)) bad.Add($"{snaps.Count} -> {back.Count}: {text.Replace("\n", "\\n")}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(8)) + $"\n(total {bad.Count})");
    }
}

/// <summary>The history against a list and an index, under random pushes, pops, moves and limits.</summary>
public class RandomizedHistoryTests
{
    static RouterEntry E(int n) => new(new object(), new object(), RouteableArgs.Create().WithRoute("/" + n));

    [Fact]
    public void History_follows_a_simple_model()
    {
        var r = new Random(99);
        var bad = new List<string>();
        for (var run = 0; run < 300; run++)
        {
            int? max = r.Next(3) == 0 ? r.Next(1, 5) : null;
            var h = new RouterHistory(max);
            var model = new List<RouterEntry>(); var idx = -1;
            var log = "";
            for (var step = 0; step < 60; step++)
            {
                var op = r.Next(8);
                log += op;
                switch (op)
                {
                    case 0:
                    case 1:
                    case 2:
                        var e = E(step);
                        if (idx < model.Count - 1) model.RemoveRange(idx + 1, model.Count - idx - 1);
                        model.Add(e); if (max is { } mm && model.Count > mm) model.RemoveAt(0);
                        idx = model.Count - 1; h.Push(e); break;
                    case 3: if (idx > 0) { model.RemoveRange(idx, model.Count - idx); idx--; } h.Pop(); break;
                    case 4: if (idx > 0) idx--; h.MoveBack(); break;
                    case 5: if (idx >= 0 && idx < model.Count - 1) idx++; h.MoveForward(); break;
                    case 6: if (model.Count > 1) { model.RemoveRange(1, model.Count - 1); idx = 0; } h.MoveToRoot(); break;
                    case 7: var to = r.Next(-1, model.Count + 1); if (to >= 0 && to < model.Count) idx = to; h.MoveTo(to); break;
                }
                var snap = h.Snapshot();
                if (!snap.SequenceEqual(model) || h.CurrentIndex != idx || h.CanGoBack != (idx > 0) || h.CanGoForward != (idx >= 0 && idx < model.Count - 1)
                    || (max is { } cap && snap.Length > cap))
                { bad.Add($"run {run} step {step} ops={log} max={max} idx={h.CurrentIndex}/{idx} count={snap.Length}/{model.Count}"); break; }
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(5)));
    }
}

/// <summary>The router under random routes (some refused or failing) and moves: the history and the disposal of the pages stay right.</summary>
public class RandomizedRouterTests
{
    [Fact]
    public async Task Router_keeps_the_history_and_the_disposal_consistent()
    {
        var bad = new List<string>();
        for (var seed = 0; seed < 150 && bad.Count < 5; seed++)
        {
            var r = new Random(seed);
            var router = CreateRouter();
            var log = new CallLog();
            var model = new List<Page>(); var idx = -1;
            var everShown = new List<Page>();
            var ops = "";
            for (var step = 0; step < 40; step++)
            {
                var op = r.Next(9); ops += op;
                try
                {
                    switch (op)
                    {
                        case 0:
                        case 1:
                        case 2:
                            {
                                var p = new Page(new RecordingViewModel($"vm{step}", log), new RecordingView($"view{step}", log));
                                var mode = r.Next(8);
                                if (mode == 0) p.ViewModel.InitializeFails = new Exception("init");
                                if (mode == 1) p.View.EnterFails = new Exception("enter");
                                if (mode == 2 && model.Count > 0) model[idx].ViewModel.CanLeave = false;
                                var res = await router.GoAsync(p.View, p.ViewModel, null, TestContext.Current.CancellationToken);
                                if (mode == 2 && model.Count > 0) model[idx].ViewModel.CanLeave = true;
                                if (res.Success)
                                {
                                    if (idx < model.Count - 1) model.RemoveRange(idx + 1, model.Count - idx - 1);
                                    model.Add(p); idx = model.Count - 1; everShown.Add(p);
                                }
                                break;
                            }
                        case 3: { var ok = idx > 0; var res = await router.GoBackAsync(TestContext.Current.CancellationToken); if (res.Success != ok) bad.Add($"seed {seed} step {step} back success={res.Success} expected {ok} ops={ops}"); if (res.Success) idx--; break; }
                        case 4: { var ok = idx >= 0 && idx < model.Count - 1; var res = await router.ForwardAsync(TestContext.Current.CancellationToken); if (res.Success != ok) bad.Add($"seed {seed} step {step} forward success={res.Success} expected {ok} ops={ops}"); if (res.Success) idx++; break; }
                        case 5: { var ok = idx > 0; var res = await router.PopAsync(TestContext.Current.CancellationToken); if (res.Success != ok) bad.Add($"seed {seed} step {step} pop success={res.Success} expected {ok} ops={ops}"); if (res.Success) { model.RemoveRange(idx, model.Count - idx); idx--; } break; }
                        case 6: { var ok = idx > 0; var res = await router.GoBackToRootAsync(TestContext.Current.CancellationToken); if (res.Success != ok) bad.Add($"seed {seed} step {step} root success={res.Success} expected {ok} ops={ops}"); if (res.Success) { model.RemoveRange(1, model.Count - 1); idx = 0; } break; }
                        case 7: { if (model.Count == 0) break; var to = r.Next(0, model.Count); var ok = to != idx; var res = await router.MoveToAsync(to, TestContext.Current.CancellationToken); if (res.Success != ok) bad.Add($"seed {seed} step {step} moveto success={res.Success} expected {ok} ops={ops}"); if (res.Success) idx = to; break; }
                        case 8: { if (r.Next(4) != 0) break; await router.EraseHistoryAsync(TestContext.Current.CancellationToken); model.Clear(); idx = -1; break; }
                    }
                }
                catch (Exception e)
                {
                    // a failed push throws: the model is unchanged
                    if (op > 2) bad.Add($"seed {seed} step {step} op {op} threw {e.GetType().Name}: {e.Message} ops={ops}");
                }

                var h = router.History;
                var cur = h.Entries.Select(e => e.ViewModel).ToList();
                if (!cur.SequenceEqual(model.Select(p => (object)p.ViewModel)) || h.CurrentIndex != idx)
                { bad.Add($"seed {seed} step {step} history differs: {string.Join(",", cur.Select(c => ((RecordingViewModel)c).Name))} idx={h.CurrentIndex} vs model {string.Join(",", model.Select(p => p.ViewModel.Name))} idx={idx} ops={ops}"); break; }
                foreach (var p in model)
                    if (p.ViewModel.DestroyCount != 0 || p.View.DestroyCount != 0) bad.Add($"seed {seed} step {step} live page {p.ViewModel.Name} destroyed ops={ops}");
                foreach (var p in everShown.Where(p => !model.Contains(p)))
                    if (p.ViewModel.DestroyCount != 1 || p.View.DestroyCount != 1) bad.Add($"seed {seed} step {step} removed page {p.ViewModel.Name} destroyed vm={p.ViewModel.DestroyCount} view={p.View.DestroyCount} ops={ops}");
                if (bad.Count > 0) break;
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(6)));
    }
}

/// <summary>The text of a coordination (<c>/region:Name?k=v/..</c>) read back gives the same segments, whatever the characters of names and values.</summary>
public class RandomizedCoordinationTests
{
    static readonly string[] P = ["a", "B", " ", "/", "?", "&", "=", "%", ":", "#", "+", "é", "日", "~", ".", "..", "'", "\"", "0", "%2F", "{", "}", "\t", ";"];
    static string R(Random r, int m = 5) { var n = r.Next(1, m); var s = ""; for (var i = 0; i < n; i++) s += P[r.Next(P.Length)]; return s; }
    [Fact]
    public void Coordination_text_round_trips()
    {
        var r = new Random(5); var bad = new List<string>();
        for (var i = 0; i < 3000; i++)
        {
            var segs = new List<RouteSegment>();
            for (var j = 0; j < r.Next(1, 4); j++)
            {
                if (r.Next(5) == 0) { segs.Add(new RouteSegment("..", r.Next(2) == 0 ? null : R(r), RouteSegmentKind.Back)); continue; }
                var ps = new List<KeyValuePair<string, object?>>();
                for (var k = 0; k < r.Next(0, 3); k++) ps.Add(new(R(r), r.Next(4) == 0 ? null : R(r)));
                var nm = R(r); if (nm == "..") nm = "x"; segs.Add(new RouteSegment(nm, r.Next(2) == 0 ? null : R(r), RouteSegmentKind.Push, ps));
            }
            var c = new RouteCoordination(segs, r.Next(2) == 0);
            var text = c.ToString();
            if (!RouteCoordination.TryParse(text, out var back, out var err)) { bad.Add($"PARSE FAIL {text}: {err.Message}"); continue; }
            if (back.Absolute != c.Absolute || back.Segments.Count != c.Segments.Count) { bad.Add($"SHAPE {text}"); continue; }
            for (var j = 0; j < segs.Count; j++)
            {
                var a = segs[j]; var b = back.Segments[j];
                var pa = string.Join("&", (a.Parameters ?? []).Select(p => p.Key + "=" + p.Value));
                var pb = string.Join("&", (b.Parameters ?? []).Select(p => p.Key + "=" + p.Value));
                if (a.Name != b.Name || a.Region != b.Region || a.Kind != b.Kind || pa != pb) bad.Add($"SEG {text}: [{a.Name}|{a.Region}|{a.Kind}|{pa}] vs [{b.Name}|{b.Region}|{b.Kind}|{pb}]");
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Distinct().Take(8)) + $"\n(total {bad.Count})");
    }
}