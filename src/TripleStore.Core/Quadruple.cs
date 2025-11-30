using System.Runtime.InteropServices;

namespace TripleStore.Core;

public class Quadruple<TBaseNumberType> where TBaseNumberType : unmanaged
{
    private static UriRegistry EffectiveIndex { get => RdfCompressionContext.Instance.UriRegistry; }

    public Quadruple(TBaseNumberType g,
                     TBaseNumberType s,
                     TBaseNumberType p,
                     TBaseNumberType o)
    {
        int x = SizeOf<TBaseNumberType>();
    }

    public static int SizeOf<T>() where T : unmanaged
    {
        return Marshal.SizeOf(default(T));
    }
}
