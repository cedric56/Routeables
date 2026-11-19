using Routeables.Abstraction.Routing;

namespace Routeables.Tests.Pages;

/// <summary>The view models of the tests that hold nothing but what their constructor is given. A test with a behaviour of its own (a hook, a counter) keeps its view model.</summary>

public sealed class BackFirstVm;
public sealed class BackSecondVm;
public sealed class BlockedVm;
public sealed class ConfirmVm;
public sealed class CtHomeVm;
public sealed class DocsVm(string? path = null) { public string? Path { get; } = path; }
public sealed class FirstViewModel;
public sealed class FirstVm;
public sealed class FormVm;
public sealed class GuardAdminVm;
public sealed class GuardHomeVm;
public sealed class GuardLoginVm;
public sealed class HelloVm;
public sealed class HomeVm;
public sealed class IntroVm;
public sealed class IsoVm;
public sealed class KeptFirstVm;
public sealed class KeptSecondVm;
public sealed class LinkFirstVm;
public sealed class LinkSecondVm;
public sealed class ListVm;
public sealed class LoginVm;
public sealed class LostVm;
public sealed class MbAVm;
public sealed class MbBVm;
public sealed class MbCVm;
public sealed class MenuVm;
public sealed class MeterHomeVm;
public sealed class MeterItemVm(int id) { public int Id { get; } = id; }
public sealed class MissingVm;
public sealed class MsLoginVm;
public sealed class MsOtherVm;
public sealed class MsProductVm;
public sealed class MsSecretVm;
public sealed class NamedVm(string name) { public string Name { get; } = name; }
public sealed class NotFoundVm;
public sealed class OpenVm;
public sealed class OrdersViewModel;
public sealed class OrdersVm;
public sealed class OrphanVm;
public sealed class OtherVm;
public sealed class PageVm(int id) { public int Id { get; } = id; }
public sealed class QoGridVm;
public sealed class QoListVm;
public sealed class RaItemVm;
public sealed class RandomPageVm;
public sealed class RootVm;
public sealed class SaProductVm;
public sealed class SecondViewModel;
public sealed class SecondVm;
public sealed class SegBlockedVm;
public sealed class SegVm;
public sealed class SlugVm(string slug) { public string Slug { get; } = slug; }
public sealed class SoHomeVm;
public sealed class SoLinkVm;
public sealed class StartVm;
public sealed class StubVm;
public sealed class TabVm;
public sealed class TgHomeVm;
