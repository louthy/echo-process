using LanguageExt;

namespace Echo.CoreProcesses
{
    internal class Root
    {
        public static ProcessId Startup<RT>() where RT : struct, HasEcho<RT>
        {
            system = ActorSystemAff<RT>.actorCreate<Unit, object>(root, Config.SystemProcessName, publish, null, ProcessFlags.Default);
        }

        public static Unit Setup()
        {
            system = ActorSystemAff<RT>.actorCreate<object>(root, Config.SystemProcessName, publish, null, ProcessFlags.Default);
        }
    }
}