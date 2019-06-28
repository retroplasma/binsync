using System;
using System.Collections.Generic;
using Binsync.Core;

namespace Tests
{
	public static class Extensions
	{
		public static T FORK<T>(this T v, Action<T> action)
		{
			action(v);
			return v;
		}

		public static T DEBUG<T>(this T v, string m = "")
		{
#if DEBUG
			return PRINT(v, m);
#else
			return v;
#endif
		}

		public static T PRINT<T>(this T v, string m = "")
		{
			Constants.Logger.Log("DEBUG variable: '{0}' with msg '{1}'", v, m);
			return v;
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<X, Z> Then<X, Y, Z>(this Func<X, Y> f, Func<Y, Z> g)
		{
			return x => x.Compose(f, g);
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static void Do<Y>(this Y data, Action<Y> f)
		{
			f(data);
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<Y, Z>(this Y data, Func<Y, Z> f)
		{
			return f(data);
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<X, Y, Z>(this X data, Func<X, Y> f, Func<Y, Z> g)
		{
			return g(data.Compose(f));
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<W, X, Y, Z>(this W data, Func<W, X> f, Func<X, Y> g, Func<Y, Z> h)
		{
			return h(data.Compose(f, g));
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<V, W, X, Y, Z>(this V data, Func<V, W> f, Func<W, X> g, Func<X, Y> h, Func<Y, Z> i)
		{
			return i(data.Compose(f, g, h));
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<U, V, W, X, Y, Z>(this U data, Func<U, V> f, Func<V, W> g, Func<W, X> h, Func<X, Y> i, Func<Y, Z> j)
		{
			return j(data.Compose(f, g, h, i));
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<T, U, V, W, X, Y, Z>(this T data, Func<T, U> f, Func<U, V> g, Func<V, W> h, Func<W, X> i, Func<X, Y> j, Func<Y, Z> k)
		{
			return k(data.Compose(f, g, h, i, j));
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Z Compose<S, T, U, V, W, X, Y, Z>(this S data, Func<S, T> f, Func<T, U> g, Func<U, V> h, Func<V, W> i, Func<W, X> j, Func<X, Y> k, Func<Y, Z> l)
		{
			return l(data.Compose(f, g, h, i, j, k));
		}


		/*
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<T1, T2> Curry<T1,T2>(this Func<T1,T2> func)
		{
			return func;
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<T1, Func<T2,T3>> Curry<T1,T2,T3>(this Func<T1,T2,T3> func)
		{
			return t1 => t2 => func(t1, t2);
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<T1, Func<T2, Func<T3,T4>>> Curry<T1,T2,T3,T4>(this Func<T1,T2,T3,T4> func)
		{
			return t1 => t2 => t3 => func(t1, t2, t3);
		}

		// Uncurrying

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<T1, T2> UnCurry<T1,T2>(this Func<T1, T2> func)
		{
			return func;
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<T1,T2,T3> UnCurry<T1,T2,T3>(this Func<T1, Func<T2,T3>> func)
		{
			return (t1, t2) => func(t1)(t2);
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static Func<T1,T2,T3,T4> UnCurry<T1,T2,T3,T4>(this Func<T1, Func<T2, Func<T3,T4>>> func)
		{
			return (t1, t2, t3) => func(t1)(t2)(t3);
		}
*/
		public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
		{
			if (action == null)
				return;

			foreach (var item in items)
			{
				action(item);
			}
		}

		public static void ForEach2<T1, T2>(this IEnumerable<T1> items, Func<T1, T2> func)
		{
			items.ForEach(x => { func(x); return; });
		}

		public static void ForEach<T>(this IEnumerable<T> items)
		{
			items.ForEach(x => { });
		}
	}
}

