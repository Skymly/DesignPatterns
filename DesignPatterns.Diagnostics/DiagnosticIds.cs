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
}
