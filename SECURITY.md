# Security policy

## Supported versions

Routeables is young and has one maintainer: security fixes go to the latest release only. Pin the version you use and read the release notes before you update.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability.

Report it privately with GitHub: **Security → Report a vulnerability** on the repository (private vulnerability reporting). Say which package and version, what you did, what you expected and what happened; a small project that shows it helps most.

You get an answer within a week. When the report is confirmed, the fix is released first and the report is made public afterwards, with credit if you want it.

## What is in scope

- The route parser and the way values of an address reach view models and views (`RouteParser`, the binding of constructor arguments and properties).
- Deep links (`RouteableDeepLinks`, `RouteableLinkInbox`) and the address synchronisation of the web hosts (`RouteableUrlSync`).
- Anything that makes a route guard (`IRouteGuard`) or a `CanLeaveAsync` run when it should not, or be skipped.

Out of scope: a guard or a deep-link scheme list that your application did not set up (see [system integration](docs/guides/system-integration.md)), and the sample applications, which are examples and not hardened services.
