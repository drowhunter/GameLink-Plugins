using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SharedLib
{
    internal class Helper
    {
        internal static IEnumerable<(string key, float value)> GetInputs<T>(T data)
        {
            foreach (var field in (data?.GetType() ?? typeof(T)).GetFields())
            {
                if (field.FieldType.IsPrimitive)
                    yield return (field.Name, GetFloat(field, data));
                else
                    foreach (var (k, v) in GetInputs(field.GetValue(data)))
                        yield return (field.Name + "." + k, v);
            }

            float GetFloat(FieldInfo f, object? data = null)
            {
                var retval = data == null ? 0 : (float)Convert.ChangeType(f.GetValue(data), typeof(float));
                return retval;
            }
        }
        

        
    }
}
