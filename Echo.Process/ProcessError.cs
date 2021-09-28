using System;
using Echo.ActorSys2.Configuration;
using LanguageExt.Common;

namespace Echo
{
    public static class ProcessError
    {
        public static Error ProcessSetup(string self, Exception inner) =>
            Error.New(79010000, $"Process failed to start: {self}", inner);
        
        public static Error ProcessAlreadyExists(ProcessId pid) =>
            Error.New(79010001, $"Process already exists: {pid}");
        
        public static Error ProcessDoesNotExist(ProcessId pid) =>
            Error.New(79010002, $"Process does not exist: {pid}");
        
        public static Error ProcessParentDoesNotExist(ProcessId pid) =>
            Error.New(79010003, $"Parent of process ({pid}) does not exist");
        
        public static Error SystemAlreadyExists(SystemName name) =>
            Error.New(79010004, $"Process-system already exists: {name}");
        
        public static Error SystemDoesNotExist(SystemName name) =>
            Error.New(79010005, $"Process-system does not exist: {name}");
        
        public static readonly Error NoSystemsRunning =
            Error.New(79010006, $"No process-systems running");

        public static Error ChildAlreadyExists(ProcessId parent, ProcessName child) =>
            Error.New(79010007, $"Child process `{child}` already exists in process: {parent}");
        
        public static Error QueueFull =
            Error.New(80010000, "Queue full");
        
        public static Error StateNotSet =
            Error.New(80010001, "State not set");
        
        public static readonly Error MustBeCalledWithinProcessContext =
            Error.New(80010002, $"should be used from within a process' message or setup function");
        
        public static readonly Error MustBeCalledOutsideProcessContext =
            Error.New(80010003, $"should only be used outside of a a process' message or setup function");
        
        public static Error TopLevelBindingAlreadyExists(Loc loc, string what) =>
            Error.New(90010004, $"{loc}: top-level binding already exists: '{what}'");
 
        public static Error UndefinedBinding(Loc loc, string what) =>
            Error.New(90010005, $"{loc}: undefined: '{what}'");
        
        public static Error NoTypeRecordedForVariable(Loc loc, string name) =>
            Error.New(90010006, $"{loc}: no type recorded for variable: '{name}'");
        
        public static Error WrongTypeOfBindingForVariable(Loc loc, string name) =>
            Error.New(90010006, $"{loc}: wrong type of binding for variable: '{name}'");
        
        public static readonly Error NoRuleApplies =
            Error.New(90010007, $"no rule applies");

        public static Error IfBranchesIncompatible(Loc loc) =>
            Error.New(90010008, $"{loc}: arms of conditional have different types");

        public static Error GuardNotBoolean(Loc loc) =>
            Error.New(90010009, $"{loc}: guard of conditional not a boolean");
        
        public static Error UnknownCase(Loc loc, string name) =>
            Error.New(90010010, $"{loc}: unknown case: {name}");
        
        public static Error MissingCase(Loc loc, string name) =>
            Error.New(90010011, $"{loc}: not all cases have been defined: {name}");
        
        public static Error BranchesOfCaseHaveNoCommonType(Loc loc) =>
            Error.New(90010012, $"{loc}: branches of case have no common type");
        
        public static Error ExpectedVariantType(Loc loc) =>
            Error.New(90010013, $"{loc}: expected variant type");
        
        public static Error ParameterTypeMismatch(Loc loc, Ty expected, Ty got) =>
            Error.New(90010014, $"{loc}: parameter type mismatch, expected: {expected.Show()}, got: {got.Show()}");
        
        public static Error ParameterKindMismatch(Loc loc, Kind expected, Kind got) =>
            Error.New(90010014, $"{loc}: parameter kind mismatch, expected: {expected.Show()}, got: {got.Show()}");
        
        public static Error FunctionTypeExpected(Loc loc) =>
            Error.New(90010015, $"{loc}: function type expected");
        
        public static Error AscribeMismatch(Loc loc, Ty expected, Ty got) =>
            Error.New(90010016, $"{loc}: body of as-term does not have the expected type, expected: {expected}, got: {got}");

        public static Error FieldNotMemberOfType(Loc loc, string member) =>
            Error.New(90010017, $"{loc}: record member not defined: " + member);
        
        public static Error ExpectedRecordType(Loc loc) =>
            Error.New(90010018, $"{loc}: expected record type");
        
        public static Error BodyIncompatibleWithDomain(Loc loc) =>
            Error.New(90010019, $"{loc}: result of body not compatible with domain");

        public static Error CaseTypeMismatch(Loc loc, string name) =>
            Error.New(90010020, $"{loc}: case type mismatch: {name}");

        public static Error AnnotationNotVariantType(Loc loc) =>
            Error.New(90010021, $"{loc}: annotation is not a variant type");
         
        public static Error ElementsOfArrayHaveNoCommonType(Loc loc) =>
            Error.New(90010022, $"{loc}: branches of array have no common type");
        
        public static Error ProcessTypeInvalid(Loc loc, Ty got) =>
            Error.New(90010023, $"{loc}: expected process type, got: {got.Show()}");
        
        public static Error RecordTypeInvalid(Loc loc, Ty got) =>
            Error.New(90010024, $"{loc}: expected record type, got: {got.Show()}");
        
        public static Error ClusterTypeInvalid(Loc loc, Ty got) =>
            Error.New(90010025, $"{loc}: expected cluster type, got: {got.Show()}");
        
        public static Error StrategyTypeInvalid(Loc loc, Ty got) =>
            Error.New(90010026, $"{loc}: expected strategy type, got: {got.Show()}");
        
        public static Error RouterTypeInvalid(Loc loc, Ty got) =>
            Error.New(90010027, $"{loc}: expected router type, got: {got.Show()}");
        
        public static Error RequiredAttributeMissing(Loc loc, string name) =>
            Error.New(90010028, $"{loc}: required attribute missing: {name}");
        
        public static Error IncorrectTypeForAttribute(Loc loc, string name, Ty got, Ty expected) =>
            Error.New(90010029, $"{loc}: incorrect type for attribute `{name}`, expected: {expected.Show()}, got: {got.Show()}");
        
        public static Error InvalidTypeInferred(Loc loc, string name, Ty got, Ty expected) =>
            Error.New(90010030, $"{loc}: invalid type inferred for `{name}`, expected: {expected.Show()}, got: {got.Show()}");
        
        public static Error InvalidTypesInferred(Loc loc, string name, Ty got1, Ty got2, Ty expected) =>
            Error.New(90010031, $"{loc}: invalid type inferred for `{name}`, expected: {expected.Show()}, got: {got1.Show()} and {got2.Show()}");
        
        public static Error InvalidTypesInferred(Loc loc, string name, Ty got1, Ty got2, string expected) =>
            Error.New(90010032, $"{loc}: invalid type inferred for `{name}`, expected: {expected}, got: {got1.Show()} and {got2.Show()}");
        
        public static Error InvalidComparisonType(Loc loc, string name, Ty got1, Ty got2) =>
            Error.New(90010033, $"{loc}: `{name}` operands have incompatible types: {got1.Show()} and {got2.Show()}");
        
        public static Error NonVariableBinding(Loc loc, string name) =>
            Error.New(90010034, $"{loc}: wrong kind of binding for variable `{name}`");
        
        public static Error NoKindRecordedForVariable(Loc loc, string name) =>
            Error.New(90010035, $"{loc}: no kind recorded for variable `{name}`");
        
        public static Error StarKindExpected(Loc loc) =>
            Error.New(90010036, $"{loc}: * kind expected");
        
        public static Error ArrowKindExpected(Loc loc, Ty lam, Ty arg, Kind got) =>
            Error.New(90010037, $"{loc}: * => * kind expected when type-abstraction: {lam} is applied to type-parameter: {arg}, instead got: {got}");

        public static Error ArgumentNotRef(Loc loc) =>
            Error.New(90010038, $"{loc}: argument is not a Ref");
        
        public static Error AssignmentOperatorArgumentsIncompatible(Loc loc) =>
            Error.New(90010039, $"{loc}: assignment operator arguments incompatible");
        
        public static Error TypeArgumentHasWrongKind(Loc loc, Kind expected, Kind got) =>
            Error.New(90010040, $"{loc}: type argument has wrong kind: expected {expected}, got: {got.Show()}");
        
        public static Error UniversalTypeExpected(Loc loc) =>        
            Error.New(90010041, $"{loc}: universal type expected");
        
        public static Error TypeComponentHasWrongKind(Loc loc, Kind expected, Kind got) =>
            Error.New(90010042, $"{loc}: type component has wrong kind: expected {expected.Show()}, got: {got.Show()}");
        
        public static Error DoesNotMatchDeclaredType(Loc loc) =>
            Error.New(90010043, $"{loc}: doesn't match declared type");
        
        public static Error ExistentialTypeExpected(Loc loc) =>
            Error.New(90010044, $"{loc}: existential type expected");
         
        public static Error UndefinedVariable(Loc loc, string what) =>
            Error.New(90010045, $"{loc}: undefined variable: '{what}'");
        
        public static Error UndefinedType(Loc loc, string what) =>
            Error.New(90010046, $"{loc}: undefined type: '{what}'");
        
        public static Error VariableAlreadyExists(Loc loc, string what) =>
            Error.New(90010047, $"{loc}: variable already exists: {what}");
        
        public static Error TypeAlreadyExists(Loc loc, string what) =>
            Error.New(90010048, $"{loc}: type already exists: {what}");
    }
}