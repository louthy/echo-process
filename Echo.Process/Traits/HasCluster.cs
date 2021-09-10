using LanguageExt.Effects.Traits;

namespace Echo.Traits
{
    public interface HasCluster<out RT> : HasCancel<RT>
        where RT : struct, HasCluster<RT>
    {
        
    }
}