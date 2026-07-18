namespace DesignPatterns.Diagnostics;

/// <summary>
/// Central diagnostic identifier constants for DesignPatterns compile-time components.
/// </summary>
public static class DiagnosticIds
{
    public const string GenerateSingletonNotPartial = "DP001";
    public const string GenerateSingletonInvalidTarget = "DP002";
    public const string RegisterStrategyDuplicateKey = "DP003";
    public const string RegisterStrategyContractMismatch = "DP004";
    public const string HandlerOrderDuplicateOrder = "DP005";
    public const string RegisterStrategyUnregisteredImplementation = "DP006";
    public const string RegisterStrategyMissingParameterlessConstructor = "DP007";
    public const string HandlerOrderContractMismatch = "DP008";
    public const string HandlerOrderMissingParameterlessConstructor = "DP009";
    public const string CompositePartDuplicateKey = "DP010";
    public const string CompositePartUnknownParentKey = "DP011";
    public const string CompositePartCycle = "DP012";
    public const string CompositePartContractMismatch = "DP013";
    public const string CompositePartMissingParameterlessConstructor = "DP014";
    public const string CompositePartMissingBuildable = "DP015";
    public const string DecoratorDuplicateOrder = "DP016";
    public const string DecoratorContractMismatch = "DP017";
    public const string DecoratorMissingDecoratorInterface = "DP018";
    public const string DecoratorMissingParameterlessConstructor = "DP019";
    public const string RegisterFactoryDuplicateKey = "DP020";
    public const string RegisterFactoryContractMismatch = "DP021";
    public const string RegisterFactoryMissingParameterlessConstructor = "DP022";
    public const string RegisterFactoryUnregisteredImplementation = "DP023";
    public const string HandlerOrderUnregisteredImplementation = "DP024";
    public const string RegistryKeyNotRegistered = "DP025";
    public const string PluginRegistryDuplicateKeyAcrossAssemblies = "DP033";
    public const string StateTransitionDuplicateEdge = "DP026";
    public const string StateTransitionInvalidStateMember = "DP027";
    public const string StateTransitionInvalidTriggerMember = "DP028";
    public const string StateTransitionInvalidInitialState = "DP029";
    public const string StateMachineHolderInvalid = "DP030";
    public const string StateTransitionIsolatedState = "DP031";
    public const string StateTransitionGuardMethodNotFound = "DP032";
    public const string StateTransitionGuardMethodNotStatic = "DP034";
    public const string StateTransitionGuardMethodWrongSignature = "DP035";
    public const string StateTransitionInvalidLiteralEdge = "DP036";
    public const string StateTransitionActionMethodNotFound = "DP037";
    public const string StateTransitionActionMethodNotStatic = "DP038";
    public const string StateTransitionActionMethodWrongSignature = "DP039";
    public const string CompositePartDiNodeNotRegistered = "DP040";
    public const string CompositePartVisitorCoverageIncomplete = "DP041";
    public const string DecoratorAsyncSignatureMismatch = "DP042";
    public const string DecoratorDiNotResolvable = "DP043";
    public const string EventHandlerUnregisteredImplementation = "DP044";
    public const string RegisterEventHandlerDuplicateOnSameClass = "DP045";
    public const string RegisterEventHandlerContractMismatch = "DP046";
    public const string StrategyGuardMethodNotFound = "DP047";
    public const string StrategyGuardMethodNotStatic = "DP048";
    public const string StrategyGuardMethodWrongSignature = "DP049";
    public const string HandlerOrderGuardMethodNotFound = "DP050";
    public const string HandlerOrderGuardMethodNotStatic = "DP051";
    public const string HandlerOrderGuardMethodWrongSignature = "DP052";
    public const string FactoryAsyncSignatureMismatch = "DP053";
    public const string FactoryPoolSizeInvalid = "DP054";
    public const string FactoryPoolSizeTooLarge = "DP055";
    public const string StateHierarchyCycle = "DP056";
    public const string StateParentInvalidMember = "DP057";
    public const string StateParentSelfReference = "DP058";
    public const string StateParentOrphanParent = "DP059";
    public const string DiLifetimeCaptiveDependency = "DP060";
    public const string DiLifetimeWasteful = "DP061";
    public const string CaptiveDependency = "DP062";
    public const string CompositeTreeMaxDepthExceeded = "DP063";
    public const string CompositeChildTypeNotAllowed = "DP064";
    public const string CompositeNodeCountExceeded = "DP065";
    public const string FactoryDelegateCaptiveDependency = "DP066";
    public const string GenerateSingletonInitializeAsyncInvalid = "DP067";
    public const string GenerateSingletonDiDoubleRegistration = "DP068";
    public const string GenerateSingletonNonThreadSafeMutableState = "DP069";
    public const string StaticMutableSingleton = "DP070";
    public const string StaticMutableSingletonDiDoubleRegistration = "DP071";
}
