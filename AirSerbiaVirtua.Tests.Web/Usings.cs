global using NUnit.Framework;

// bUnit needs a fresh context per test; NUnit reuses one fixture instance by
// default, so force a new instance (and thus a new BunitContext) per test case.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
