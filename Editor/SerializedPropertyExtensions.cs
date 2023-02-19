using System;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace EnesShahn.Extensions
{
	public static class SerializedPropertyExtensions
	{
		private static string pattern = @".*\[(\d*)\]";
		private static Regex rx = new Regex(pattern);

		public static object GetObjectValue(this SerializedProperty property)
		{
			var iterator = GetSerializedPropertyEnumerator(property);
			object currentObject = null;
			while (iterator.MoveNext())
			{
				currentObject = iterator.Current.Value;
			}
			return currentObject;
		}
		public static T GetObjectValue<T>(this SerializedProperty property)
		{
			return (T)GetObjectValue(property);
		}
		public static Type GetPropertyFieldType(this SerializedProperty property)
		{
			var iterator = GetSerializedPropertyEnumerator(property);
			while (iterator.MoveNext())
			{
				if (iterator.Current.Property.propertyPath == property.propertyPath)
					return iterator.Current.FieldType;
			}
			return null;
		}

		public static bool IsAnyAncestorOfType(this SerializedProperty property, params Type[] checkTypes)
		{
			var propValue = property.GetObjectValue();
			var iterator = GetSerializedPropertyEnumerator(property);
			while (iterator.MoveNext())
			{
				if (iterator.Current.Value.Equals(propValue)) break;
				var currentObjectTypeCheck = iterator.Current.Value.GetType().GetGenericTypeDefinitionIfGeneric();
				foreach (var checkType in checkTypes)
				{
					if (currentObjectTypeCheck == checkType)
					{
						return true;
					}
				}
			}
			return false;
		}
		public static bool IsAnyAncestorArray(this SerializedProperty property)
		{
			var propValue = property.GetObjectValue();
			var iterator = GetSerializedPropertyEnumerator(property);
			bool isArray = false;
			while (iterator.MoveNext())
			{
				if (iterator.Current.Value.Equals(propValue)) break;
				var currentObjectTypeCheck = iterator.Current.Value.GetType().GetGenericTypeDefinitionIfGeneric();
				if (currentObjectTypeCheck.IsArray)
				{
					isArray = true;
				}
			}
			return isArray;
		}
		public static bool IsAnyArrayAncestorNotOfType(this SerializedProperty property, Type checkType)
		{
			var iterator = GetSerializedPropertyEnumerator(property);
			while (iterator.MoveNext())
			{
				var currentObjectType = iterator.Current.Value.GetType().GetGenericTypeDefinitionIfGeneric();

				if (currentObjectType == checkType)
				{
					iterator.MoveNext();
					continue;
				}

				if (currentObjectType == typeof(List<>) || currentObjectType.IsArray)
					return true;
			}

			return false;
		}
		public static SerializedProperty GetParentSerializedProperty(this SerializedProperty property)
		{
			var propValue = property.GetObjectValue();
			var iterator = GetSerializedPropertyEnumerator(property);
			SerializedProperty parentSP = null;
			while (iterator.MoveNext())
			{
				if (iterator.Current.Value.Equals(propValue)) break;
				parentSP = iterator.Current.Property;
			}
			return parentSP;
		}
		public static IEnumerator<SPInfo> GetSerializedPropertyEnumerator(this SerializedProperty property)
		{
			SerializedObject serializedObject = property.serializedObject;
			UnityEngine.Object targetObject = serializedObject.targetObject;
			var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

			string[] propPathSplit = property.propertyPath.Split('.');

			Type containerObjectType = targetObject.GetType();
			FieldInfo currentObjectFieldInfo = containerObjectType.GetField(propPathSplit[0], bindingFlags);
			object currentObject = currentObjectFieldInfo.GetValue(targetObject);
			SerializedProperty currentProperty = serializedObject.FindProperty(propPathSplit[0]);

			yield return new SPInfo(currentProperty.Copy(), currentObject, currentObjectFieldInfo.FieldType);
			for (int i = 1; i < propPathSplit.Length; i++)
			{
				var currentObjectType = currentObject.GetType();
				Type fieldType = null;
				if (propPathSplit[i].Equals("Array"))
				{
					i++;
					var pathRegexMatch = rx.Match(propPathSplit[i]);
					int index = Convert.ToInt32(pathRegexMatch.Groups[1].Value);

					Array array = null;
					if (currentObjectType.IsGenericType && currentObjectType.GetGenericTypeDefinition() == typeof(List<>))
					{
						fieldType = currentObjectType;
						currentObjectFieldInfo = currentObjectType.GetField("_items", bindingFlags);
						array = (Array)currentObjectFieldInfo.GetValue(currentObject);
					}
					else if (currentObjectType.IsArray)
					{
						fieldType = currentObject.GetType();
						array = (Array)currentObject;
					}

					if (array == null)
					{
						UnityEngine.Debug.Log("Miserable failure");
					}

					currentProperty = currentProperty.GetArrayElementAtIndex(index);
					currentObject = array.GetValue(index);
				}
				else
				{
					currentProperty = currentProperty.FindPropertyRelative(propPathSplit[i]);
					currentObjectFieldInfo = currentObjectType.GetField(propPathSplit[i], bindingFlags);
					fieldType = currentObjectFieldInfo.FieldType;
					currentObject = currentObjectFieldInfo.GetValue(currentObject);
				}

				yield return new SPInfo(currentProperty.Copy(), currentObject, fieldType);
			}
			yield break;
		}

		public static bool HasAttribute(this SerializedProperty property, Type attributeType)
		{
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var members = property.serializedObject.targetObject.GetType().GetMember(property.propertyPath, flags);

			foreach (var member in members)
			{
				if (member.CustomAttributes.Count() == 0) continue;
				var attribute = member.GetCustomAttribute(attributeType);
				if (attribute == null) continue;
				if (member.Name != property.propertyPath) continue;
				return true;
			}
			return false;
		}
		public static Attribute GetAttribute(this SerializedProperty property, Type attributeType)
		{
			BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			var members = property.serializedObject.targetObject.GetType().GetMember(property.propertyPath, flags);

			foreach (var member in members)
			{
				if (member.CustomAttributes.Count() == 0) continue;
				var attribute = member.GetCustomAttribute(attributeType);
				if (attribute == null) continue;
				if (member.Name != property.propertyPath) continue;
				return attribute;
			}
			return null;
		}

		public static void SetChildrenExpand(this SerializedProperty property, bool isExpanded)
		{
			var parent = property.Copy();
			parent.isExpanded = isExpanded;
			var childIterator = parent.Copy();
			if (childIterator.Next(true))
			{
				do
				{
					SetChildrenExpand(childIterator.Copy(), isExpanded);
				}
				while (childIterator.Next(false));
			}
		}

	}

	public class SPInfo
	{
		public SerializedProperty Property;
		public object Value;
		public Type FieldType;

		public SPInfo(SerializedProperty property, object value, Type type)
		{
			Property = property;
			Value = value;
			FieldType = type;
		}
	}
}