using LanguageExt.Effects.Traits;

namespace Echo.Traits
{
    public interface HasEcho<RT> where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        
    }
}