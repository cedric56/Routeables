namespace Routeables.Comparison.Prism;

// One view class per page, as in the other programs. According to Prism's source (not checked here), navigation looks for a page's view among
// the region's views by the TYPE the name was registered with. With one class for every name, a request for P01 could find the view P00 built
// and show it again, so the numbers would be those of a single view shown over and over. The job counts the views each library builds, and the
// driver checks the type of the view shown after each navigation.
public sealed class P00Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P00") { }
public sealed class P01Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P01") { }
public sealed class P02Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P02") { }
public sealed class P03Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P03") { }
public sealed class P04Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P04") { }
public sealed class P05Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P05") { }
public sealed class P06Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P06") { }
public sealed class P07Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P07") { }
public sealed class P08Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P08") { }
public sealed class P09Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P09") { }
public sealed class P10Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P10") { }
public sealed class P11Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P11") { }
public sealed class P12Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P12") { }
public sealed class P13Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P13") { }
public sealed class P14Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P14") { }
public sealed class P15Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P15") { }
public sealed class P16Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P16") { }
public sealed class P17Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P17") { }
public sealed class P18Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P18") { }
public sealed class P19Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P19") { }
public sealed class P20Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P20") { }
public sealed class P21Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P21") { }
public sealed class P22Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P22") { }
public sealed class P23Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P23") { }
public sealed class P24Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P24") { }
public sealed class P25Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P25") { }
public sealed class P26Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P26") { }
public sealed class P27Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P27") { }
public sealed class P28Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P28") { }
public sealed class P29Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P29") { }
public sealed class P30Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P30") { }
public sealed class P31Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P31") { }
public sealed class P32Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P32") { }
public sealed class P33Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P33") { }
public sealed class P34Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P34") { }
public sealed class P35Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P35") { }
public sealed class P36Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P36") { }
public sealed class P37Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P37") { }
public sealed class P38Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P38") { }
public sealed class P39Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "P39") { }
public sealed class C0Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C0") { }
public sealed class C1Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C1") { }
public sealed class C2Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C2") { }
public sealed class C3Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C3") { }
public sealed class C4Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C4") { }
public sealed class C5Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C5") { }
public sealed class C6Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C6") { }
public sealed class C7Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C7") { }
public sealed class C8Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C8") { }
public sealed class C9Page(PrismPageViewModel viewModel) : PrismPage(viewModel, "C9") { }
public sealed class ListPage(PrismPageViewModel viewModel) : PrismPage(viewModel, "List") { }
public sealed class DetailPage(PrismPageViewModel viewModel) : PrismPage(viewModel, "Detail") { }
public sealed class SignInPage(PrismPageViewModel viewModel) : PrismPage(viewModel, "SignIn") { }
