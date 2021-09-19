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
            Error.New(90010005, $"{loc}: variable undefined: '{what}'");
        
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
        
        public static Error ParameterTypeMismatch(Loc loc) =>
            Error.New(90010014, $"{loc}: parameter type mismatch");
        
        public static Error FunctionTypeExpected(Loc loc) =>
            Error.New(90010015, $"{loc}: function type expected");
        
        public static Error AscribeMismatch(Loc loc) =>
            Error.New(90010016, $"{loc}: body of as-term does not have the expected type");

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
            Error.New(90010012, $"{loc}: branches of array have no common type");
    }
}