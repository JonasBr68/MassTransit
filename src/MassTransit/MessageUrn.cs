namespace MassTransit
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;


    [Serializable]
    public class MessageUrn :
        Uri
    {
        static readonly ConcurrentDictionary<Type, Cached> _cache = new ConcurrentDictionary<Type, Cached>();
        static readonly ConcurrentDictionary<string, Cached> _urnTypeCache = new ConcurrentDictionary<string, Cached>(StringComparer.OrdinalIgnoreCase);
        static readonly ConcurrentDictionary<string, Type> _messageTypesCache = new ConcurrentDictionary<string, Type>();
        public static MessageUrn ForType<T>() => MessageUrnCache<T>.Urn;
        public static string ForTypeString<T>() => MessageUrnCache<T>.UrnString;

        public static MessageUrn ForType(Type type)
        {
            if (type.ContainsGenericParameters)
                throw new ArgumentException("A message type may not contain generic parameters", nameof(type));

            Cached cached = _cache.GetOrAdd(type, _ => (Cached)Activator.CreateInstance(typeof(Cached<>).MakeGenericType(type)));
            return _urnTypeCache.GetOrAdd(cached.UrnString, cached).Urn;
        }

        public static string ForTypeString(Type type)
        {
            Cached cached = _cache.GetOrAdd(type, _ => (Cached)Activator.CreateInstance(typeof(Cached<>).MakeGenericType(type)));
            return _urnTypeCache.GetOrAdd(cached.UrnString, cached).UrnString;
        }

        public static Type ForMessageUrnString(string urnString)
        {
            Cached cached = null;
            if (!_urnTypeCache.TryGetValue(urnString, out cached))
            {
                MessageUrn urn = new MessageUrn(urnString);
                Type type = GetType(urn);
                if (type == null)
                    return null;

                cached = _cache.GetOrAdd(type, _ => (Cached)Activator.CreateInstance(typeof(Cached<>).MakeGenericType(type)));
                _urnTypeCache.TryAdd(cached.UrnString, cached);
            }
            return cached.MessageType;
        }
        public static Type GetType(MessageUrn urn)
        {
            string urnTypeString = urn.GetUrnTypeString();
            return GetTypeFromTypeString(urnTypeString);
        }
        public static Type GetTypeFromTypeString(string urnTypeString)
        {
            var recipe = Deconstruct(urnTypeString);
            if (recipe.Root == null)
                return null;

            return BuildCompleteType(recipe);
        }

        private static Type BuildCompleteType(TypeRecipe recipe)
        {
            if (recipe.GenericTypeArguments?.Length > 0)
            {
                if (!recipe.Root.IsGenericType)
                    throw new ArgumentException("Invalid TypeRecipe");

                var typeArguments = recipe.GenericTypeArguments.Select(t => BuildCompleteType(t));
                if (typeArguments.Any(ta => ta == null))
                    return null;
                return recipe.Root.MakeGenericType(typeArguments.ToArray());
            }
            else
            {
                return recipe.Root;
            }

        }

        public static Type GetLoadedType(string typeName, bool throwOnError)
        {
            if (_messageTypesCache.TryGetValue(typeName, out var foundType))
                return foundType;

            var type = Type.GetType(typeName);
            if (type != null) return type;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName, false, true);
                if (type != null)
                {
                    _messageTypesCache.TryAdd(typeName, type);
                    return type;
                }
            }

            if (throwOnError)
                throw new ArgumentOutOfRangeException($"{typeName} not found in any loaded assembly!");
            return null;
        }

        static class MessageUrnCache<T>
        {
            internal static readonly MessageUrn Urn;
            internal static readonly string UrnString;

            static MessageUrnCache()
            {
                Urn = new MessageUrn(GetUrnForType(typeof(T)));
                UrnString = Urn.ToString();
            }
        }


        interface Cached
        {
            MessageUrn Urn { get; }
            string UrnString { get; }
            Type MessageType { get; }
        }


        class Cached<T> :
            Cached
        {
            public MessageUrn Urn => MessageUrnCache<T>.Urn;
            public string UrnString => MessageUrnCache<T>.UrnString;
            public Type MessageType => typeof(T);
        }


        MessageUrn(string uriString)
            : base(uriString)
        {
        }

        protected MessageUrn(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }

        public string GetUrnTypeString()
        {
            if (!string.Equals("urn", Scheme, StringComparison.OrdinalIgnoreCase)
                || !Segments[0].StartsWith("message:", StringComparison.OrdinalIgnoreCase)
                || Segments.Length == 0)
            {
                throw new ArgumentException("Not a vaild MessageUrn!");
            }

            return Segments[0].Substring("message:".Length);

        }
        public void Deconstruct(out string name, out string ns, out string assemblyName)
        {
            Deconstruct(this.GetUrnTypeString(), out name, out ns, out assemblyName);
        }
        public static void Deconstruct(string urnTypeString, out string name, out string ns, out string assemblyName)
        {
            name = null;
            ns = null;
            assemblyName = null;

            string[] names = urnTypeString.Split(':');
            if (names.Length == 1)
            {
                name = names[0];
            }
            else if (names.Length == 2)
            {
                name = names[1];
                ns = names[0];
            }
            else if (names.Length >= 3)
            {
                name = names[1];
                ns = names[0];
                assemblyName = names[2];
            }

        }
        public class TypeRecipe
        {
            public Type Root;
            public TypeRecipe[] GenericTypeArguments;
        }
        public static TypeRecipe Deconstruct(string urnTypeString)
        {
            //MassTransit.Tests:GM[[MassTransit.Tests:MessageUrnSpecs+X],[MassTransit.Tests:MessageUrnSpecs+X]]
            int indexOfColon = urnTypeString.IndexOf(':');
            if (indexOfColon > -1)
            {
                string ns = urnTypeString.Substring(0, indexOfColon);
                string rest = urnTypeString.Substring(indexOfColon + 1);
                int indexOfGenericsSeparator = rest.IndexOf('[');
                if(indexOfGenericsSeparator == -1)
                {
                    return new TypeRecipe() { Root = GetLoadedType($"{ns}.{rest}", false) };
                }
                string genericName = rest.Substring(0, indexOfGenericsSeparator);
                //rest = "G[[MassTransit.Tests:MessageUrnSpecs+X]]"
                //GM[[MassTransit.Tests:MessageUrnSpecs+X],[MassTransit.Tests:MessageUrnSpecs+X]]
                string[] nestedTypeStrings = rest.Substring(indexOfGenericsSeparator + 1, rest.Length - (indexOfGenericsSeparator + 2)).Split(',');
                
                return new TypeRecipe() { Root = GetLoadedType($"{ns}.{genericName}`{nestedTypeStrings.Length}", false), GenericTypeArguments = nestedTypeStrings.Select(s => Deconstruct(s.Substring(1, s.Length-2))).ToArray() };
            }
            else
            {
                throw new ArgumentException($"Not a valid urnTypeString: {urnTypeString}");
            }
        }
        static string GetUrnForType(Type type)
        {
            var sb = new StringBuilder("urn:message:");

            return GetMessageName(sb, type, true);
        }

        static string GetMessageName(StringBuilder sb, Type type, bool includeScope)
        {
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsGenericParameter)
                return "";

            if (includeScope && typeInfo.Namespace != null)
            {
                string ns = typeInfo.Namespace;
                sb.Append(ns);

                sb.Append(':');
            }

            if (typeInfo.IsNested)
            {
                GetMessageName(sb, typeInfo.DeclaringType, false);
                sb.Append('+');
            }

            if (typeInfo.IsGenericType)
            {
                var name = typeInfo.GetGenericTypeDefinition().Name;

                //remove `1
                int index = name.IndexOf('`');
                if (index > 0)
                    name = name.Remove(index);
                //

                sb.Append(name);
                sb.Append('[');

                Type[] arguments = typeInfo.GetGenericArguments();
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');

                    sb.Append('[');
                    GetMessageName(sb, arguments[i], true);
                    sb.Append(']');
                }

                sb.Append(']');
            }
            else
                sb.Append(typeInfo.Name);

            return sb.ToString();
        }
    }
}
