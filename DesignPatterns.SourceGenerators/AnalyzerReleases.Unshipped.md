### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DP001    | DesignPatterns.Generators | Error    | GenerateSingleton requires a partial class
DP002    | DesignPatterns.Generators | Error    | GenerateSingleton target is invalid
DP003    | DesignPatterns.Generators | Error    | Duplicate strategy key for the same contract
DP004    | DesignPatterns.Generators | Error    | Strategy implementation does not implement contract
DP005    | DesignPatterns.Generators | Error    | Duplicate handler order for the same context
DP007    | DesignPatterns.Generators | Error    | Strategy implementation missing public parameterless constructor
DP008    | DesignPatterns.Generators | Error    | Handler does not implement IHandler for context
DP009    | DesignPatterns.Generators | Error    | Handler missing public parameterless constructor
DP010    | DesignPatterns.Generators | Error    | Duplicate composite key for the same contract
DP011    | DesignPatterns.Generators | Error    | Unknown composite parent key
DP012    | DesignPatterns.Generators | Error    | Composite parent-key cycle
DP013    | DesignPatterns.Generators | Error    | Composite part does not implement contract
DP014    | DesignPatterns.Generators | Error    | Composite part missing public parameterless constructor
DP015    | DesignPatterns.Generators | Error    | Composite part missing ICompositeBuildable implementation
DP016    | DesignPatterns.Generators | Error    | Duplicate decorator order for the same service contract
DP017    | DesignPatterns.Generators | Error    | Decorator does not implement service contract
DP018    | DesignPatterns.Generators | Error    | Decorator does not implement IDecorator for service contract
DP019    | DesignPatterns.Generators | Error    | Decorator missing public parameterless constructor
DP020    | DesignPatterns.Generators | Error    | Duplicate factory key for the same contract
DP021    | DesignPatterns.Generators | Error    | Factory implementation does not implement contract
DP022    | DesignPatterns.Generators | Error    | Factory implementation missing public parameterless constructor
DP006    | DesignPatterns.Analyzers | Info     | Strategy implementation missing RegisterStrategy attribute
DP023    | DesignPatterns.Analyzers | Info     | Factory implementation missing RegisterFactory attribute
DP024    | DesignPatterns.Analyzers | Info     | Handler implementation missing HandlerOrder attribute
DP025    | DesignPatterns.Analyzers | Info     | Registry key is not registered for contract
DP033    | DesignPatterns.Analyzers | Error    | Duplicate strategy key across referenced provider assemblies
DP026    | DesignPatterns.Generators | Error    | Duplicate state transition edge
DP027    | DesignPatterns.Generators | Error    | Transition state is not a declared enum member
DP028    | DesignPatterns.Generators | Error    | Transition trigger is not a declared enum member
DP029    | DesignPatterns.Generators | Error    | Initial state is not a declared enum member
DP030    | DesignPatterns.Generators | Error    | StateMachine holder must be a static partial class
DP031    | DesignPatterns.Generators | Info     | State is never used as a transition source
DP032    | DesignPatterns.Generators | Error    | Guard method not found on holder class
DP034    | DesignPatterns.Generators | Error    | Guard method is not static
DP035    | DesignPatterns.Generators | Error    | Guard method has wrong signature
DP036    | DesignPatterns.Analyzers | Info     | State transition edge is not declared
DP037    | DesignPatterns.Generators | Error    | Action method not found on holder class
DP038    | DesignPatterns.Generators | Error    | Action method is not static
DP039    | DesignPatterns.Generators | Error    | Action method has wrong signature
DP040    | DesignPatterns.Generators | Error    | Composite node not registered with DI container
DP042    | DesignPatterns.Generators | Error    | Async decorator has wrong DecorateAsync signature
DP043    | DesignPatterns.Generators | Warning  | Decorator not resolvable from DI container
DP044    | DesignPatterns.Analyzers | Info     | Event handler implementation missing RegisterEventHandler attribute
DP045    | DesignPatterns.Generators | Error    | Duplicate RegisterEventHandler on same class for same event type
DP046    | DesignPatterns.Generators | Error    | Event handler does not implement IEventHandler for event type
DP047    | DesignPatterns.Generators | Error    | Strategy guard method not found on implementation class
DP048    | DesignPatterns.Generators | Error    | Strategy guard method is not static
DP049    | DesignPatterns.Generators | Error    | Strategy guard method has wrong signature
DP050    | DesignPatterns.Generators | Error    | Handler guard method not found on handler class
DP051    | DesignPatterns.Generators | Error    | Handler guard method is not static
DP052    | DesignPatterns.Generators | Error    | Handler guard method has wrong signature
