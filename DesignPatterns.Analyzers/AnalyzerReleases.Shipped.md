## Release 0.2.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DP006    | DesignPatterns.Analyzers | Info     | Strategy implementation missing RegisterStrategy attribute
DP023    | DesignPatterns.Analyzers | Info     | Factory implementation missing RegisterFactory attribute
DP024    | DesignPatterns.Analyzers | Info     | Handler implementation missing HandlerOrder attribute
DP025    | DesignPatterns.Analyzers | Info     | Registry key is not registered for contract
DP033    | DesignPatterns.Analyzers | Error    | Duplicate strategy key across referenced provider assemblies
DP036    | DesignPatterns.Analyzers | Info     | State transition edge is not declared
DP044    | DesignPatterns.Analyzers | Info     | Event handler implementation missing RegisterEventHandler attribute
DP060    | DesignPatterns.Analyzers | Warning  | DI captive dependency: registry lifetime exceeds implementation lifetime
DP061    | DesignPatterns.Analyzers | Info     | DI lifetime mismatch: implementation lifetime exceeds registry lifetime
DP062    | DesignPatterns.Analyzers | Warning  | Singleton captive dependency on Scoped/Transient service
