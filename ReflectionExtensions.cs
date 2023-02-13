using System.Collections.Generic;
using System;
using System.Reflection;

namespace EnesShahn.Extensions
{
    public static class ReflectionExtensions
    {
        public static Type[] GetChildTypes(this Type type, bool includeSelf = true)
        {
            List<Type> typesList = new List<Type>();
            if (includeSelf) typesList.Add(type);

            var types = Assembly.GetAssembly(type).GetTypes();
            foreach (var t in types)
            {
                if (t.IsSubclassOf(type)) typesList.Add(t);
            }

            return typesList.ToArray();
        }
		public static Type GetGenericTypeDefinitionIfGeneric(this Type type)
		{
			return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
		}
    }
}