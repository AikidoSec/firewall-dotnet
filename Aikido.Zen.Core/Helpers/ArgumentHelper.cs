using System;
using System.Collections.Generic;
using System.Reflection;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class to work with method arguments.
    /// </summary>
    public static class ArgumentHelper
    {
        /// <summary>
        /// Builds a dictionary mapping parameter names to their values from the provided arguments array.
        /// This method correctly handles various parameter types including optional, 'out', and 'params' arrays.
        /// </summary>
        /// <param name="__args">The array of arguments passed to the method.</param>
        /// <param name="__originalMethod">The reflection info of the original method.</param>
        /// <returns>A dictionary of parameter names and their corresponding values.</returns>
        public static Dictionary<string, object> BuildArgumentDictionary(object[] __args, MethodBase __originalMethod)
        {
            var argumentDictionary = new Dictionary<string, object>();
            var parameters = __originalMethod.GetParameters();
            int argIndex = 0;
            __args = __args ?? Array.Empty<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramName = param.Name;

                if (param.IsOut)
                {
                    argumentDictionary[paramName] = null; // 'out' parameters are not in __args
                    continue;
                }

                if (param.IsDefined(typeof(ParamArrayAttribute), false))
                {
                    int paramArrayLength = __args.Length - argIndex;
                    var elementType = param.ParameterType.GetElementType() ?? typeof(object);
                    var paramArray = Array.CreateInstance(elementType, paramArrayLength);

                    if (paramArrayLength > 0)
                    {
                        for (int j = 0; j < paramArrayLength; j++)
                        {
                            paramArray.SetValue(__args[argIndex + j], j);
                        }
                    }

                    argumentDictionary[paramName] = paramArray;
                    break;
                }

                if (argIndex < __args.Length)
                {
                    argumentDictionary[paramName] = __args[argIndex++];
                }
                else if (param.IsOptional)
                {
                    argumentDictionary[paramName] = param.DefaultValue;
                }
                else
                {
                    argumentDictionary[paramName] = null;
                }
            }

            return argumentDictionary;
        }
    }
}
