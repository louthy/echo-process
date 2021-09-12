using Echo.Config;
using LanguageExt;
using LanguageExt.UnitsOfMeasure;

namespace Echo.ActorSys2.Configuration
{
    public record ClusterConfig(
        Option<string> Alias, 
        string NodeName, 
        string Role, 
        string Connection, 
        string Database, 
        Option<string> Env, 
        Option<string> UserEnv, 
        bool Default);
}