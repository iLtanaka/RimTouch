using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RimTouch
{
    internal static class ReflectionGuard
    {
        private static readonly HashSet<string> LoggedMessages = new HashSet<string>();

        public static FieldInfo Field(Type type, string name)
        {
            return Field(type, name, true);
        }

        public static FieldInfo Field(Type type, string name, bool logMissing)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null && logMissing)
            {
                LogOnce("missing-field:" + type.FullName + "." + name, "Missing reflected field " + type.FullName + "." + name + ".");
            }
            return field;
        }

        public static MethodInfo Method(Type type, string name)
        {
            return Method(type, name, true);
        }

        public static MethodInfo Method(Type type, string name, bool logMissing)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null && logMissing)
            {
                LogOnce("missing-method:" + type.FullName + "." + name, "Missing reflected method " + type.FullName + "." + name + ".");
            }
            return method;
        }

        public static MethodInfo Method(Type type, string name, Type[] parameters)
        {
            return Method(type, name, parameters, true);
        }

        public static MethodInfo Method(Type type, string name, Type[] parameters, bool logMissing)
        {
            MethodInfo method = AccessTools.Method(type, name, parameters);
            if (method == null && logMissing)
            {
                LogOnce("missing-method:" + type.FullName + "." + name, "Missing reflected method " + type.FullName + "." + name + ".");
            }
            return method;
        }

        public static PropertyInfo Property(Type type, string name)
        {
            return Property(type, name, true);
        }

        public static PropertyInfo Property(Type type, string name, bool logMissing)
        {
            PropertyInfo property = AccessTools.Property(type, name);
            if (property == null && logMissing)
            {
                LogOnce("missing-property:" + type.FullName + "." + name, "Missing reflected property " + type.FullName + "." + name + ".");
            }
            return property;
        }

        public static bool TryGetField<T>(FieldInfo field, object instance, out T value)
        {
            value = default(T);
            if (field == null)
            {
                return false;
            }

            try
            {
                object rawValue = field.GetValue(instance);
                if (rawValue is T)
                {
                    value = (T)rawValue;
                    return true;
                }

                if (rawValue == null && !typeof(T).IsValueType)
                {
                    value = (T)rawValue;
                    return true;
                }

                LogOnce(
                    "bad-field-type:" + field.DeclaringType.FullName + "." + field.Name,
                    "Reflected field " + field.DeclaringType.FullName + "." + field.Name + " had unexpected value type.");
            }
            catch (Exception ex)
            {
                LogOnce(
                    "get-field-failed:" + field.DeclaringType.FullName + "." + field.Name,
                    "Failed to read reflected field " + field.DeclaringType.FullName + "." + field.Name + ": " + ex.GetType().Name + ": " + ex.Message);
            }

            return false;
        }

        public static bool TrySetField(FieldInfo field, object instance, object value)
        {
            if (field == null)
            {
                return false;
            }

            try
            {
                field.SetValue(instance, value);
                return true;
            }
            catch (Exception ex)
            {
                LogOnce(
                    "set-field-failed:" + field.DeclaringType.FullName + "." + field.Name,
                    "Failed to write reflected field " + field.DeclaringType.FullName + "." + field.Name + ": " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        public static bool TryGetProperty<T>(PropertyInfo property, object instance, out T value)
        {
            value = default(T);
            if (property == null)
            {
                return false;
            }

            try
            {
                object rawValue = property.GetValue(instance, null);
                if (rawValue is T)
                {
                    value = (T)rawValue;
                    return true;
                }

                if (rawValue == null && !typeof(T).IsValueType)
                {
                    value = (T)rawValue;
                    return true;
                }

                LogOnce(
                    "bad-property-type:" + property.DeclaringType.FullName + "." + property.Name,
                    "Reflected property " + property.DeclaringType.FullName + "." + property.Name + " had unexpected value type.");
            }
            catch (Exception ex)
            {
                LogOnce(
                    "get-property-failed:" + property.DeclaringType.FullName + "." + property.Name,
                    "Failed to read reflected property " + property.DeclaringType.FullName + "." + property.Name + ": " + ex.GetType().Name + ": " + ex.Message);
            }

            return false;
        }

        public static bool TryInvoke(MethodInfo method, object instance, object[] parameters)
        {
            object ignored;
            return TryInvoke(method, instance, parameters, out ignored);
        }

        public static bool TryInvoke(MethodInfo method, object instance, object[] parameters, out object result)
        {
            result = null;
            if (method == null)
            {
                return false;
            }

            try
            {
                result = method.Invoke(instance, parameters);
                return true;
            }
            catch (Exception ex)
            {
                LogOnce(
                    "invoke-failed:" + method.DeclaringType.FullName + "." + method.Name,
                    "Failed to invoke reflected method " + method.DeclaringType.FullName + "." + method.Name + ": " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static void LogOnce(string key, string message)
        {
            if (LoggedMessages.Add(key))
            {
                Log.Warning("[RimTouch] " + message);
            }
        }
    }
}
