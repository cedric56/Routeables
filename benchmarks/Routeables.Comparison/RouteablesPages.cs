// Generated once by hand-run script: 40 pages of the menu (5 with a constructor dependency) and 10 with a route parameter.
using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Registrations;

namespace Routeables.Comparison;

public abstract class RVm(string name) { public string Name { get; } = name; }

public sealed class P00View : IRouteableFor { public object? Context { get; set; } }
public sealed class P00Vm() : RVm("P00");
public sealed class P01View : IRouteableFor { public object? Context { get; set; } }
public sealed class P01Vm() : RVm("P01");
public sealed class P02View : IRouteableFor { public object? Context { get; set; } }
public sealed class P02Vm() : RVm("P02");
public sealed class P03View : IRouteableFor { public object? Context { get; set; } }
public sealed class P03Vm() : RVm("P03");
public sealed class P04View : IRouteableFor { public object? Context { get; set; } }
public sealed class P04Vm() : RVm("P04");
public sealed class P05View : IRouteableFor { public object? Context { get; set; } }
public sealed class P05Vm() : RVm("P05");
public sealed class P06View : IRouteableFor { public object? Context { get; set; } }
public sealed class P06Vm() : RVm("P06");
public sealed class P07View : IRouteableFor { public object? Context { get; set; } }
public sealed class P07Vm() : RVm("P07");
public sealed class P08View : IRouteableFor { public object? Context { get; set; } }
public sealed class P08Vm() : RVm("P08");
public sealed class P09View : IRouteableFor { public object? Context { get; set; } }
public sealed class P09Vm() : RVm("P09");
public sealed class P10View : IRouteableFor { public object? Context { get; set; } }
public sealed class P10Vm() : RVm("P10");
public sealed class P11View : IRouteableFor { public object? Context { get; set; } }
public sealed class P11Vm() : RVm("P11");
public sealed class P12View : IRouteableFor { public object? Context { get; set; } }
public sealed class P12Vm() : RVm("P12");
public sealed class P13View : IRouteableFor { public object? Context { get; set; } }
public sealed class P13Vm() : RVm("P13");
public sealed class P14View : IRouteableFor { public object? Context { get; set; } }
public sealed class P14Vm() : RVm("P14");
public sealed class P15View : IRouteableFor { public object? Context { get; set; } }
public sealed class P15Vm() : RVm("P15");
public sealed class P16View : IRouteableFor { public object? Context { get; set; } }
public sealed class P16Vm() : RVm("P16");
public sealed class P17View : IRouteableFor { public object? Context { get; set; } }
public sealed class P17Vm() : RVm("P17");
public sealed class P18View : IRouteableFor { public object? Context { get; set; } }
public sealed class P18Vm() : RVm("P18");
public sealed class P19View : IRouteableFor { public object? Context { get; set; } }
public sealed class P19Vm() : RVm("P19");
public sealed class P20View : IRouteableFor { public object? Context { get; set; } }
public sealed class P20Vm() : RVm("P20");
public sealed class P21View : IRouteableFor { public object? Context { get; set; } }
public sealed class P21Vm() : RVm("P21");
public sealed class P22View : IRouteableFor { public object? Context { get; set; } }
public sealed class P22Vm() : RVm("P22");
public sealed class P23View : IRouteableFor { public object? Context { get; set; } }
public sealed class P23Vm() : RVm("P23");
public sealed class P24View : IRouteableFor { public object? Context { get; set; } }
public sealed class P24Vm() : RVm("P24");
public sealed class P25View : IRouteableFor { public object? Context { get; set; } }
public sealed class P25Vm() : RVm("P25");
public sealed class P26View : IRouteableFor { public object? Context { get; set; } }
public sealed class P26Vm() : RVm("P26");
public sealed class P27View : IRouteableFor { public object? Context { get; set; } }
public sealed class P27Vm() : RVm("P27");
public sealed class P28View : IRouteableFor { public object? Context { get; set; } }
public sealed class P28Vm() : RVm("P28");
public sealed class P29View : IRouteableFor { public object? Context { get; set; } }
public sealed class P29Vm() : RVm("P29");
public sealed class P30View : IRouteableFor { public object? Context { get; set; } }
public sealed class P30Vm(IClock clock) : RVm("P30") { public IClock Clock { get; } = clock; }
public sealed class P31View : IRouteableFor { public object? Context { get; set; } }
public sealed class P31Vm(IClock clock) : RVm("P31") { public IClock Clock { get; } = clock; }
public sealed class P32View : IRouteableFor { public object? Context { get; set; } }
public sealed class P32Vm(IClock clock) : RVm("P32") { public IClock Clock { get; } = clock; }
public sealed class P33View : IRouteableFor { public object? Context { get; set; } }
public sealed class P33Vm(IClock clock) : RVm("P33") { public IClock Clock { get; } = clock; }
public sealed class P34View : IRouteableFor { public object? Context { get; set; } }
public sealed class P34Vm(IClock clock) : RVm("P34") { public IClock Clock { get; } = clock; }
public sealed class P35View : IRouteableFor { public object? Context { get; set; } }
public sealed class P35Vm() : RVm("P35");
public sealed class P36View : IRouteableFor { public object? Context { get; set; } }
public sealed class P36Vm() : RVm("P36");
public sealed class P37View : IRouteableFor { public object? Context { get; set; } }
public sealed class P37Vm() : RVm("P37");
public sealed class P38View : IRouteableFor { public object? Context { get; set; } }
public sealed class P38Vm() : RVm("P38");
public sealed class P39View : IRouteableFor { public object? Context { get; set; } }
public sealed class P39Vm() : RVm("P39");
public sealed class C0View : IRouteableFor { public object? Context { get; set; } }
public sealed class C0Vm(int id) : RVm($"C0:{id}");
public sealed class C1View : IRouteableFor { public object? Context { get; set; } }
public sealed class C1Vm(int id) : RVm($"C1:{id}");
public sealed class C2View : IRouteableFor { public object? Context { get; set; } }
public sealed class C2Vm(int id) : RVm($"C2:{id}");
public sealed class C3View : IRouteableFor { public object? Context { get; set; } }
public sealed class C3Vm(int id) : RVm($"C3:{id}");
public sealed class C4View : IRouteableFor { public object? Context { get; set; } }
public sealed class C4Vm(int id) : RVm($"C4:{id}");
public sealed class C5View : IRouteableFor { public object? Context { get; set; } }
public sealed class C5Vm(int id) : RVm($"C5:{id}");
public sealed class C6View : IRouteableFor { public object? Context { get; set; } }
public sealed class C6Vm(int id) : RVm($"C6:{id}");
public sealed class C7View : IRouteableFor { public object? Context { get; set; } }
public sealed class C7Vm(int id) : RVm($"C7:{id}");
public sealed class C8View : IRouteableFor { public object? Context { get; set; } }
public sealed class C8Vm(int id) : RVm($"C8:{id}");
public sealed class C9View : IRouteableFor { public object? Context { get; set; } }
public sealed class C9Vm(int id) : RVm($"C9:{id}");

public static class RouteablesPages
{
    public static void Register(IServiceCollection services)
    {
        const ServiceLifetime t = ServiceLifetime.Transient;
        services.RegisterRoute((_, _) => new P00View(), (_, _) => new P00Vm(), t, t, "/p00");
        services.RegisterRoute((_, _) => new P01View(), (_, _) => new P01Vm(), t, t, "/p01");
        services.RegisterRoute((_, _) => new P02View(), (_, _) => new P02Vm(), t, t, "/p02");
        services.RegisterRoute((_, _) => new P03View(), (_, _) => new P03Vm(), t, t, "/p03");
        services.RegisterRoute((_, _) => new P04View(), (_, _) => new P04Vm(), t, t, "/p04");
        services.RegisterRoute((_, _) => new P05View(), (_, _) => new P05Vm(), t, t, "/p05");
        services.RegisterRoute((_, _) => new P06View(), (_, _) => new P06Vm(), t, t, "/p06");
        services.RegisterRoute((_, _) => new P07View(), (_, _) => new P07Vm(), t, t, "/p07");
        services.RegisterRoute((_, _) => new P08View(), (_, _) => new P08Vm(), t, t, "/p08");
        services.RegisterRoute((_, _) => new P09View(), (_, _) => new P09Vm(), t, t, "/p09");
        services.RegisterRoute((_, _) => new P10View(), (_, _) => new P10Vm(), t, t, "/p10");
        services.RegisterRoute((_, _) => new P11View(), (_, _) => new P11Vm(), t, t, "/p11");
        services.RegisterRoute((_, _) => new P12View(), (_, _) => new P12Vm(), t, t, "/p12");
        services.RegisterRoute((_, _) => new P13View(), (_, _) => new P13Vm(), t, t, "/p13");
        services.RegisterRoute((_, _) => new P14View(), (_, _) => new P14Vm(), t, t, "/p14");
        services.RegisterRoute((_, _) => new P15View(), (_, _) => new P15Vm(), t, t, "/p15");
        services.RegisterRoute((_, _) => new P16View(), (_, _) => new P16Vm(), t, t, "/p16");
        services.RegisterRoute((_, _) => new P17View(), (_, _) => new P17Vm(), t, t, "/p17");
        services.RegisterRoute((_, _) => new P18View(), (_, _) => new P18Vm(), t, t, "/p18");
        services.RegisterRoute((_, _) => new P19View(), (_, _) => new P19Vm(), t, t, "/p19");
        services.RegisterRoute((_, _) => new P20View(), (_, _) => new P20Vm(), t, t, "/p20");
        services.RegisterRoute((_, _) => new P21View(), (_, _) => new P21Vm(), t, t, "/p21");
        services.RegisterRoute((_, _) => new P22View(), (_, _) => new P22Vm(), t, t, "/p22");
        services.RegisterRoute((_, _) => new P23View(), (_, _) => new P23Vm(), t, t, "/p23");
        services.RegisterRoute((_, _) => new P24View(), (_, _) => new P24Vm(), t, t, "/p24");
        services.RegisterRoute((_, _) => new P25View(), (_, _) => new P25Vm(), t, t, "/p25");
        services.RegisterRoute((_, _) => new P26View(), (_, _) => new P26Vm(), t, t, "/p26");
        services.RegisterRoute((_, _) => new P27View(), (_, _) => new P27Vm(), t, t, "/p27");
        services.RegisterRoute((_, _) => new P28View(), (_, _) => new P28Vm(), t, t, "/p28");
        services.RegisterRoute((_, _) => new P29View(), (_, _) => new P29Vm(), t, t, "/p29");
        services.RegisterRoute((_, _) => new P30View(), (sp, _) => new P30Vm(sp.GetRequiredService<IClock>()), t, t, "/p30");
        services.RegisterRoute((_, _) => new P31View(), (sp, _) => new P31Vm(sp.GetRequiredService<IClock>()), t, t, "/p31");
        services.RegisterRoute((_, _) => new P32View(), (sp, _) => new P32Vm(sp.GetRequiredService<IClock>()), t, t, "/p32");
        services.RegisterRoute((_, _) => new P33View(), (sp, _) => new P33Vm(sp.GetRequiredService<IClock>()), t, t, "/p33");
        services.RegisterRoute((_, _) => new P34View(), (sp, _) => new P34Vm(sp.GetRequiredService<IClock>()), t, t, "/p34");
        services.RegisterRoute((_, _) => new P35View(), (_, _) => new P35Vm(), t, t, "/p35");
        services.RegisterRoute((_, _) => new P36View(), (_, _) => new P36Vm(), t, t, "/p36");
        services.RegisterRoute((_, _) => new P37View(), (_, _) => new P37Vm(), t, t, "/p37");
        services.RegisterRoute((_, _) => new P38View(), (_, _) => new P38Vm(), t, t, "/p38");
        services.RegisterRoute((_, _) => new P39View(), (_, _) => new P39Vm(), t, t, "/p39");
        services.RegisterRoute<C0View, C0Vm>(t, t, "/c0/{id:int}");
        services.RegisterRoute<C1View, C1Vm>(t, t, "/c1/{id:int}");
        services.RegisterRoute<C2View, C2Vm>(t, t, "/c2/{id:int}");
        services.RegisterRoute<C3View, C3Vm>(t, t, "/c3/{id:int}");
        services.RegisterRoute<C4View, C4Vm>(t, t, "/c4/{id:int}");
        services.RegisterRoute<C5View, C5Vm>(t, t, "/c5/{id:int}");
        services.RegisterRoute<C6View, C6Vm>(t, t, "/c6/{id:int}");
        services.RegisterRoute<C7View, C7Vm>(t, t, "/c7/{id:int}");
        services.RegisterRoute<C8View, C8Vm>(t, t, "/c8/{id:int}");
        services.RegisterRoute<C9View, C9Vm>(t, t, "/c9/{id:int}");
    }
}
