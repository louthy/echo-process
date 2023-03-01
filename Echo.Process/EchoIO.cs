using LanguageExt.Effects.Traits;
using LanguageExt.Sys.Traits;

namespace Echo;

/// <summary>
/// Placeholder Echo trait
/// </summary>
/// <typeparam name="RT">Runtime</typeparam>
public interface HasEcho<RT> : HasTime<RT>, HasFile<RT>
    where RT : 
    struct, 
    HasCancel<RT>, 
    HasEncoding<RT>,
    HasTime<RT>,
    HasFile<RT>
{
} 