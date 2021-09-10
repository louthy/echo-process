using System;
using LanguageExt.Common;

namespace Echo
{
    public static class ProcessError
    {
        public static Error ProcessSetup(string self, Exception inner) =>
            Error.New(79000000, $"Process failed to start: {self}", inner);
        
        public static Error ProcessAlreadyExists(ProcessId pid) =>
            Error.New(79000001, $"Process already exists: {pid}");
        
        public static Error ProcessDoesNotExist(ProcessId pid) =>
            Error.New(79000002, $"Process does not exist: {pid}");
        
        public static Error ProcessParentDoesNotExist(ProcessId pid) =>
            Error.New(79000003, $"Parent of process ({pid}) does not exist");
        
        public static Error SystemAlreadyExists(SystemName name) =>
            Error.New(79000004, $"Process-system already exists: {name}");
        
        public static Error SystemDoesNotExist(SystemName name) =>
            Error.New(79000005, $"Process-system does not exist: {name}");
        
        public static readonly Error NoSystemsRunning =
            Error.New(79000006, $"No process-systems running");

        public static Error ChildAlreadyExists(ProcessId parent, ProcessName child) =>
            Error.New(79000007, $"Child process `{child}` already exists in process: {parent}");
        
        public static Error QueueFull =
            Error.New(80000000, "Queue full");
        
        public static Error StateNotSet =
            Error.New(80000001, "State not set");
        
        public static Error MustBeCalledWithinProcessContext(string what) =>
            Error.New(80000002, $"'{what}' should be used from within a process' message or setup function");
        
        public static Error MustBeCalledOutsideProcessContext(string what) =>
            Error.New(80000003, $"'{what}' should only be used outside of a a process' message or setup function");
    }
}