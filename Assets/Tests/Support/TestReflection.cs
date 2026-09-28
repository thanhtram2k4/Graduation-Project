using System;
using System.Reflection;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Sets private [SerializeField] values on assets and components that expose
    /// read-only accessors, so tests can build configs without editor tooling.
    /// </summary>
    public static class TestReflection
    {
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Sets <paramref name="fieldName"/> on <paramref name="target"/>, searching base types.</summary>
        /// <exception cref="MissingFieldException">The field no longer exists (renamed in production code).</exception>
        public static void SetField(object target, string fieldName, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName, InstanceAny);
                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(
                $"{target.GetType().Name}.{fieldName} not found. If it was renamed, update the test rig to match.");
        }
    }
}
