using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TypesafeEnums
{
    public abstract class TSEnum
    {
        internal static readonly TSEnum DEFAULT_STATE = new DefaultState();

        private sealed class DefaultState : TSEnum, IComparable
        {
            internal DefaultState()
            {
            }

            public int CompareTo(object? obj)
            {
                return (obj is TSEnum other)
                    ? ReferenceEquals(this, TSEnum.DEFAULT_STATE)
                    ? ReferenceEquals(other, TSEnum.DEFAULT_STATE)
                    ? 0
                    : 1
                    : -1
                    : throw new ArgumentException($"Object must be of type {typeof(TSEnum).FullName}");
            }
        }

        internal TSEnum()
        {
        }

        public static void Switch<T>(T? state, params Case<T>[] cases) where T : TSEnum<T>
        {
            SortedDictionary<TSEnum, Action> caseMap = [];
            foreach (Case<T> c in cases)
            {
                foreach (TSEnum s in c.States)
                {
                    if (!caseMap.TryAdd(s, c.Action))
                    {
                        throw new ArgumentException($"Duplicate case for state: {s}");
                    }
                }
            }
            if (!caseMap.TryGetValue(state ?? DEFAULT_STATE, out var action))
            {
                return;
            }
            action();
        }

        public static R Switch<T, R>(T? state, params CaseResult<T, R>[] cases) where T : TSEnum<T>
        {
            SortedDictionary<TSEnum, Func<R>> caseMap = [];
            foreach (CaseResult<T, R> c in cases)
            {
                foreach(TSEnum s in c.States)
                {
                    if (!caseMap.TryAdd(s, c.Func))
                    {
                        throw new ArgumentException($"Duplicate case for state: {s}");
                    }
                }
            }
            if (!caseMap.TryGetValue(state ?? DEFAULT_STATE, out var func))
            {
                throw new InvalidOperationException("switch didn't cover all states and had no default");
            }
            return func();
        }

        public static Case<T> Case<T>(T[] states, Action action) where T : TSEnum<T>
        {
            return new Case<T>(states, action);
        }

        public static Case<T> Default<T>(Action action) where T : TSEnum<T>
        {
            return new Case<T>([TSEnum.DEFAULT_STATE], action);
        }

        public static CaseResult<T, R> Case<T, R>(T[] states, Func<R> func) where T : TSEnum<T>
        {
            return new CaseResult<T, R>(states, func);
        }

        public static CaseResult<T, R> Default<T, R>(Func<R> func) where T : TSEnum<T>
        {
            return new CaseResult<T, R>([TSEnum.DEFAULT_STATE], func);
        }
    }

    public abstract class TSEnum<T> : TSEnum, IEquatable<T>, IComparable, IComparable<T> where T : TSEnum<T>
    {
        private static readonly ConcurrentDictionary<T, string> NamesMap = new();

        static TSEnum()
        {
            RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);

            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                 .Where(f => typeof(T).IsAssignableFrom(f.FieldType));

            foreach (var field in fields)
            {
                if (field.GetValue(null) is T instance)
                {
                    NamesMap.TryAdd(instance, field.Name);
                }
            }
        }

        protected internal TSEnum() { }

        public string Name => NamesMap.TryGetValue((T)this, out var name) ? name : "UNKNOWN_STATE";

        public override sealed bool Equals(object? obj) => ReferenceEquals(this, obj);

        public bool Equals(T? other) => ReferenceEquals(this, other);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

        public int CompareTo(T? other) => other is not null
            ? string.Compare(Name, other.Name, StringComparison.Ordinal)
            : throw new ArgumentNullException(nameof(other), "Cannot compare to null");

        public int CompareTo(object? obj)
        {
            return (obj is T other)
                ? CompareTo(other)
                : (obj is TSEnum)
                ? 1
                : throw new ArgumentException($"Object must be of type {typeof(T).FullName}");
        }

        public override string ToString() => Name;

        public static T[] Values() => [.. NamesMap.Keys];
    }

    public readonly struct Case<T> where T : TSEnum<T>
    {
        internal TSEnum[] States { get; }
        internal Action Action { get; }

        internal Case(TSEnum[] states, Action action)
        {
            States = states;
            Action = action;
        }
    }

    public readonly struct CaseResult<T, R> where T : TSEnum<T>
    {
        internal TSEnum[] States { get; }
        internal Func<R> Func { get; }

        internal CaseResult(TSEnum[] states, Func<R> func)
        {
            States = states;
            Func = func;
        }
    }
}
